using System.Collections;
using System.Text;
using Unchained.Drawing.Primitives;
using Unchained.Drawing.Primitives.Fonts;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Media;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Export;

/// <summary>
///     Converts a <see cref="PresentationDocument" /> into a PDF 1.7 byte stream.
///     Each non-hidden slide becomes one PDF page at the correct dimensions.
///     Text uses embedded TrueType fonts when available; falls back to Helvetica otherwise.
///     Images are embedded as PDF image XObjects.
/// </summary>
internal static class PptxToPdfWriter
{
    // EMU → points: 1 pt = 12700 EMU
    private const double EmuToPoints = Emu.EmuToPoints;

    /// <summary>
    ///     Generates a PDF from <paramref name="document" /> and returns the raw bytes.
    /// </summary>
    public static byte[] Write(PresentationDocument document, PdfSaveOptions options)
    {
        var slides = CollectSlides(document.Slides, options);
        var writer = new PdfBuilder();
        writer.WritePdf(slides, document.SlideSize, document.Media, options);
        return writer.ToArray();
    }

    private static List<Slide> CollectSlides(SlideCollection slides, PdfSaveOptions options)
    {
        var result = new List<Slide>(slides.Count);
        result.AddRange(slides.Where(slide => !slide.IsHidden || options.IncludeHiddenSlides));

        return result;
    }

    // ── PDF builder ────────────────────────────────────────────────────────────

    private sealed class PdfBuilder
    {
        private readonly MemoryStream _buf = new();
        private readonly List<long> _offsets = [];
        private int _nextObjNum = 1;

        public byte[] ToArray() => _buf.ToArray();

        public void WritePdf(
            List<Slide> slides,
            SlideSize slideSize,
            MediaStore media,
            PdfSaveOptions options
        )
        {
            WriteRaw("%PDF-1.7\n%\u00c7\u00ec\u008f\u00a2\n");

            var catalogNum = AllocObj();
            var pagesNum = AllocObj();

            // Pre-allocate page / content object numbers
            var pageNums = slides.Select(_ => AllocObj()).ToList();
            var contentNums = slides.Select(_ => AllocObj()).ToList();

            // Structure tree for tagged PDF.
            var structTreeRootNum = AllocObj();
            // One struct element per shape per slide: pre-allocate.
            var structElemNums = slides
                .Select(static s => s.Shapes.Count(static sh => !sh.IsDecorative))
                .Select(count => Enumerable.Range(0, count).Select(_ => AllocObj()).ToList())
                .ToList();

            // Collect images across all slides
            var imageMap = new Dictionary<string, int>(); // PartUri → obj number
            foreach (var slide in slides)
                CollectImages(slide, imageMap);

            var imageObjNums = imageMap.Keys
                .ToDictionary(static k => k, _ => AllocObj());

            // Collect unique font keys used across all slides.
            // Key = "TypefaceName|Style" (e.g. "Calibri or Regular"), value = PDF resource name.
            var fontKeys = CollectFontKeys(slides);
            // Allocate PDF object triple per embedded font: FontFile2 + FontDescriptor + Font.
            var fontObjNums = new Dictionary<string, (int FileObj, int DescObj, int FontObj)>();
            foreach (var key in fontKeys.Keys) fontObjNums[key] = (AllocObj(), AllocObj(), AllocObj());
            // Fallback Helvetica for runs with no embedded font.
            var fallbackFontNum = AllocObj();

            // Reused across slides to avoid allocating a StringBuilder per iteration.
            var sb2 = new StringBuilder();

            // Write page + content objects
            for (var i = 0; i < slides.Count; i++)
            {
                options.Progress?.Report((double)i / slides.Count * 0.8);

                var slide = slides[i];
                var widthPt = slideSize.Width.Value * EmuToPoints;
                var heightPt = slideSize.Height.Value * EmuToPoints;

                var slideImages = CollectSlideImages(slide, imageObjNums);

                // Determine which fonts this slide uses.
                var slideFontKeys = CollectSlideFontKeys(slide);

                var contentBytes = BuildContentStream(
                    slide,
                    widthPt,
                    heightPt,
                    fontObjNums,
                    fontKeys,
                    slideImages,
                    structElemNums[i]
                );

                // Write page object
                StartObj(pageNums[i]);
                WriteLn($"<< /Type /Page /Parent {pagesNum} 0 R");
                WriteLn($"   /MediaBox [0 0 {widthPt:F4} {heightPt:F4}]");
                WriteLn($"   /Contents {contentNums[i]} 0 R");

                // Font resources: all embedded fonts + fallback.
                sb2.Clear();
                sb2.Append("   /Resources << /Font <<");
                sb2.Append($" /Fhv {fallbackFontNum} 0 R");
                foreach (var (key, nums) in fontObjNums)
                {
                    if (slideFontKeys.Contains(key))
                        sb2.Append($" /{fontKeys[key]} {nums.FontObj} 0 R");
                }

                sb2.Append(" >>");
                WriteLn(sb2.ToString());

                if (slideImages.Count > 0)
                {
                    Write("   /XObject <<");
                    foreach (var (name, objNum) in slideImages)
                        Write($" /{name} {objNum} 0 R");
                    Write(" >>");
                }

                WriteLn(" >> >>");
                EndObj();

                // Write content stream object
                StartObj(contentNums[i]);
                WriteLn($"<< /Length {contentBytes.Length} >>");
                WriteLn("stream");
                WriteBytes(contentBytes);
                WriteLn("\nendstream");
                EndObj();
            }

            // Write image XObjects
            foreach (var slide in slides)
                WriteSlideImages(slide, imageObjNums);

            // Write embedded font objects
            foreach (var (key, nums) in fontObjNums)
            {
                // Parse key: "TypefaceName|Style"
                var parts = key.Split('|');
                var typeface = parts[0];
                var style = parts.Length > 1
                    ? Enum.TryParse<EmbeddedFontStyle>(parts[1], out var s) ? s : EmbeddedFontStyle.Regular
                    : EmbeddedFontStyle.Regular;

                var fontData = media.FindFontData(typeface, style);
                var fontBytes = fontData?.ToArray();
                var metrics = fontBytes is not null
                    ? TrueTypeMetrics.Read(fontBytes)
                    : null;
                metrics ??= TrueTypeMetrics.HelveticaFallback;

                if (fontBytes is not null && fontBytes.Length > 0)
                {
                    // FontFile2 stream (raw TrueType bytes)
                    StartObj(nums.FileObj);
                    WriteLn($"<< /Length {fontBytes.Length} /Length1 {fontBytes.Length} >>");
                    WriteLn("stream");
                    WriteBytes(fontBytes);
                    WriteLn("\nendstream");
                    EndObj();
                }

                // FontDescriptor
                var flags = style switch
                {
                    EmbeddedFontStyle.Bold => 32 | 262144, // Serif + ForceBold
                    EmbeddedFontStyle.Italic => 32 | 64,   // Serif + Italic
                    EmbeddedFontStyle.BoldItalic => 32 | 64 | 262144,
                    _ => 32 // Serif
                };
                StartObj(nums.DescObj);
                WriteLn("<< /Type /FontDescriptor");
                WriteLn($"   /FontName /{SanitizePdfName(typeface)}");
                WriteLn($"   /Flags {flags}");
                WriteLn($"   /FontBBox [{metrics.XMin} {metrics.YMin} {metrics.XMax} {metrics.YMax}]");
                WriteLn("   /ItalicAngle 0");
                WriteLn($"   /Ascent {metrics.Ascent}");
                WriteLn($"   /Descent {metrics.Descent}");
                WriteLn($"   /CapHeight {metrics.CapHeight}");
                WriteLn($"   /StemV {metrics.StemV}");
                if (fontBytes is not null && fontBytes.Length > 0)
                    WriteLn($"   /FontFile2 {nums.FileObj} 0 R");
                WriteLn(">>");
                EndObj();

                // Font dictionary (TrueType)
                StartObj(nums.FontObj);
                WriteLn("<< /Type /Font /Subtype /TrueType");
                WriteLn($"   /BaseFont /{SanitizePdfName(typeface)}");
                WriteLn("   /Encoding /WinAnsiEncoding");
                WriteLn($"   /FontDescriptor {nums.DescObj} 0 R");
                WriteLn(">>");
                EndObj();
            }

            // Fallback Helvetica Type1 font
            StartObj(fallbackFontNum);
            WriteLn("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica");
            WriteLn("   /Encoding /WinAnsiEncoding >>");
            EndObj();

            // Write Pages tree
            StartObj(pagesNum);
            Write($"<< /Type /Pages /Count {slides.Count} /Kids [");
            foreach (var n in pageNums) Write($" {n} 0 R");
            WriteLn(" ] >>");
            EndObj();

            // Write Catalog with tagged PDF markers.
            StartObj(catalogNum);
            WriteLn($"<< /Type /Catalog /Pages {pagesNum} 0 R");
            WriteLn("   /MarkInfo << /Marked true >>");
            WriteLn($"   /StructTreeRoot {structTreeRootNum} 0 R >>");
            EndObj();

            // Write StructTreeRoot and per-shape structure elements.
            WriteStructTree(structTreeRootNum, slides, pageNums, structElemNums);

            // xref table
            var xrefOffset = _buf.Length;
            WriteLn("xref");
            WriteLn($"0 {_nextObjNum}");
            WriteLn("0000000000 65535 f ");
            foreach (var off in _offsets)
                WriteLn($"{off:D10} 00000 n ");

            WriteLn("trailer");
            WriteLn($"<< /Size {_nextObjNum} /Root {catalogNum} 0 R >>");
            WriteLn("startxref");
            WriteLn($"{xrefOffset}");
            WriteRaw("%%EOF\n");

            options.Progress?.Report(1.0);
        }

        // ── Font key collection ────────────────────────────────────────────────

        // Collects all unique "TypefaceName|Style" keys used in text runs across slides.
        // Returns a dictionary from key → PDF resource name (e.g. "F0", "F1", ...).
        private static Dictionary<string, string> CollectFontKeys(List<Slide> slides)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var slide in slides)
                CollectFontKeysFromShapes(slide.Shapes, keys);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var idx = 0;
            foreach (var key in keys)
                result[key] = $"F{idx++}";
            return result;
        }

        private static void CollectFontKeysFromShapes(ShapeCollection shapes, ISet<string> keys)
        {
            foreach (var shape in shapes)
            {
                switch (shape)
                {
                    case AutoShape auto:
                        CollectFontKeysFromFrame(auto.TextFrame, keys);
                    break;
                    case GroupShape grp:
                        CollectFontKeysFromShapes(grp.Children, keys);
                    break;
                    case TableShape table:
                    {
                        for (var r = 0; r < table.Grid.RowCount; r++)
                        for (var c = 0; c < table.Grid.ColumnCount; c++)
                            CollectFontKeysFromFrame(table.Grid[c, r].TextFrame, keys);
                        break;
                    }
                }
            }
        }

        private static void CollectFontKeysFromFrame(TextFrame frame, ISet<string> keys)
        {
            foreach (var para in frame.Paragraphs)
            foreach (var run in para.Runs)
            {
                var typeface = run.Format.LatinFont ?? TextConstants.FallbackLatinFont;
                var style = ResolveStyle(run.Format);
                keys.Add($"{typeface}|{style}");
            }
        }

        private static HashSet<string> CollectSlideFontKeys(Slide slide)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectFontKeysFromShapes(slide.Shapes, keys);
            return keys;
        }

        // ── Content stream ────────────────────────────────────────────────────

        private static byte[] BuildContentStream(
            Slide slide,
            double pageWidth,
            double pageHeight,
            IReadOnlyDictionary<string, (int FileObj, int DescObj, int FontObj)> fontObjNums,
            IReadOnlyDictionary<string, string> fontKeys,
            IReadOnlyDictionary<string, int> slideImages,
            ICollection structElemNums
        )
        {
            var sb = new StringBuilder();
            var colorScheme = slide.Master.Theme.Colors;

            // White background
            AppendLine(sb, "q");
            AppendLine(sb, "1 1 1 rg");
            AppendLine(sb, $"0 0 {pageWidth:F4} {pageHeight:F4} re f");
            AppendLine(sb, "Q");

            // Slide background fill
            WriteBackground(sb, slide, pageWidth, pageHeight, colorScheme);

            // Shapes — wrap each non-decorative shape in BDC/EMC for tagged PDF.
            var mcid = 0;
            foreach (var shape in slide.Shapes)
            {
                var isDecorative = shape.IsDecorative;
                if (!isDecorative && mcid < structElemNums.Count)
                {
                    var structType = StructTypeForShape(shape);
                    AppendLine(sb, $"/{structType} <</MCID {mcid}>> BDC");
                }
                else
                    AppendLine(sb, "/Artifact BMC");

                switch (shape)
                {
                    case AutoShape auto:
                        WriteAutoShape(
                            sb,
                            auto,
                            pageHeight,
                            colorScheme,
                            fontObjNums,
                            fontKeys
                        );
                    break;
                    case PictureShape pic:
                        WritePictureShape(sb, pic, pageHeight, slideImages);
                    break;
                    case TableShape table:
                        WriteTableShape(
                            sb,
                            table,
                            pageHeight,
                            colorScheme,
                            fontObjNums,
                            fontKeys
                        );
                    break;
                }

                AppendLine(sb, "EMC");

                if (!isDecorative) mcid++;
            }

            return Encoding.Latin1.GetBytes(sb.ToString());
        }

        private static string StructTypeForShape(Shape shape) => shape switch
        {
            PictureShape => "Figure",
            TableShape => "Table",
            _ => "P"
        };

        private static void WriteBackground(
            StringBuilder sb,
            Slide slide,
            double pageWidth,
            double pageHeight,
            ColorScheme? colorScheme
        )
        {
            FillFormat? fill = null;
            if (slide.Background.Fill.Type != FillType.None)
                fill = slide.Background.Fill;
            else if (slide.Layout.Background.Fill.Type != FillType.None)
                fill = slide.Layout.Background.Fill;
            else if (slide.Master.Background.Fill.Type != FillType.None)
                fill = slide.Master.Background.Fill;

            if (fill is null || fill.Type != FillType.Solid || fill.Solid == null)
                return;

            var (r, g, b) = ToRgbF(fill.Solid.Color.Resolve(colorScheme));
            AppendLine(sb, "q");
            AppendLine(sb, $"{r:F4} {g:F4} {b:F4} rg");
            AppendLine(sb, $"0 0 {pageWidth:F4} {pageHeight:F4} re f");
            AppendLine(sb, "Q");
        }

        private static void WriteAutoShape(
            StringBuilder sb,
            AutoShape shape,
            double pageHeight,
            ColorScheme? colorScheme,
            IReadOnlyDictionary<string, (int FileObj, int DescObj, int FontObj)> fontObjNums,
            IReadOnlyDictionary<string, string> fontKeys
        )
        {
            var x = shape.X.Value * EmuToPoints;
            var y = shape.Y.Value * EmuToPoints;
            var w = shape.Width.Value * EmuToPoints;
            var h = shape.Height.Value * EmuToPoints;
            var pdfY = pageHeight - y - h;

            AppendLine(sb, "q");

            // Fill
            var fillColor = ResolveFill(shape, colorScheme);
            if (fillColor.HasValue)
            {
                var (r, g, b) = fillColor.Value;
                AppendLine(sb, $"{r:F4} {g:F4} {b:F4} rg");
                AppendLine(sb, $"{x:F4} {pdfY:F4} {w:F4} {h:F4} re f");
            }

            // Stroke
            if (shape.Line.Fill is { Type: FillType.Solid, Solid: not null })
            {
                var (r, g, b) = ToRgbF(shape.Line.Fill.Solid.Color.Resolve(colorScheme));
                var lw = shape.Line.WidthPoints ?? 0.75;
                AppendLine(sb, $"{r:F4} {g:F4} {b:F4} RG");
                AppendLine(sb, $"{lw:F4} w");
                AppendLine(sb, $"{x:F4} {pdfY:F4} {w:F4} {h:F4} re S");
            }

            AppendLine(sb, "Q");

            WriteTextFrame(
                sb,
                shape.TextFrame,
                x,
                y,
                h,
                pageHeight,
                colorScheme,
                shape.StyleTextColor,
                fontObjNums,
                fontKeys
            );
        }

        private static void WriteTableShape(
            StringBuilder sb,
            TableShape table,
            double pageHeight,
            ColorScheme? colorScheme,
            IReadOnlyDictionary<string, (int FileObj, int DescObj, int FontObj)> fontObjNums,
            IReadOnlyDictionary<string, string> fontKeys
        )
        {
            var x = table.X.Value * EmuToPoints;
            var y = table.Y.Value * EmuToPoints;
            var w = table.Width.Value * EmuToPoints;
            var h = table.Height.Value * EmuToPoints;

            var grid = table.Grid;
            if (grid.ColumnCount == 0 || grid.RowCount == 0) return;

            var totalW = grid.ColumnWidths.Sum(static c => c.Value);
            var totalH = grid.RowHeights.Sum(static r => r.Value);
            if (totalW <= 0 || totalH <= 0) return;

            // Compute column/row edges in points.
            var colEdgesPt = new double[grid.ColumnCount + 1];
            colEdgesPt[0] = x;
            for (var c = 0; c < grid.ColumnCount; c++)
            {
                var frac = (double)grid.ColumnWidths[c].Value / totalW;
                colEdgesPt[c + 1] = colEdgesPt[c] + (frac * w);
            }

            var rowEdgesPt = new double[grid.RowCount + 1];
            rowEdgesPt[0] = y;
            for (var r = 0; r < grid.RowCount; r++)
            {
                var frac = (double)grid.RowHeights[r].Value / totalH;
                rowEdgesPt[r + 1] = rowEdgesPt[r] + (frac * h);
            }

            for (var r = 0; r < grid.RowCount; r++)
            for (var c = 0; c < grid.ColumnCount; c++)
            {
                var cell = grid[c, r];
                if (cell.IsHorizontalMergeContinuation || cell.IsVerticalMergeContinuation)
                    continue;

                var cx = colEdgesPt[c];
                var cy = rowEdgesPt[r];
                var cw = colEdgesPt[Math.Min(c + cell.ColumnSpan, grid.ColumnCount)] - cx;
                var ch = rowEdgesPt[Math.Min(r + cell.RowSpan, grid.RowCount)] - cy;
                if (cw <= 0 || ch <= 0) continue;

                var pdfCy = pageHeight - cy - ch;

                AppendLine(sb, "q");
                if (cell.Fill is { Type: FillType.Solid, Solid: not null })
                {
                    var (fr, fg, fb) = ToRgbF(cell.Fill.Solid.Color.Resolve(colorScheme));
                    AppendLine(sb, $"{fr:F4} {fg:F4} {fb:F4} rg");
                    AppendLine(sb, $"{cx:F4} {pdfCy:F4} {cw:F4} {ch:F4} re f");
                }

                // Cell border
                AppendLine(sb, "0.78 0.78 0.78 RG 0.5 w");
                AppendLine(sb, $"{cx:F4} {pdfCy:F4} {cw:F4} {ch:F4} re S");
                AppendLine(sb, "Q");

                WriteTextFrame(
                    sb,
                    cell.TextFrame,
                    cx + 2,
                    cy + 2,
                    ch - 4,
                    pageHeight,
                    colorScheme,
                    null,
                    fontObjNums,
                    fontKeys
                );
            }
        }

        private static (double r, double g, double b)? ResolveFill(Shape shape, ColorScheme? colorScheme)
        {
            if (shape.Fill is { Type: FillType.Solid, Solid: not null })
                return ToRgbF(shape.Fill.Solid.Color.Resolve(colorScheme));
            if (shape.Fill.Type == FillType.None && shape.StyleFillColor.HasValue)
                return ToRgbF(shape.StyleFillColor.Value.Resolve(colorScheme));

            return null;
        }

        private static void WritePictureShape(
            StringBuilder sb,
            PictureShape shape,
            double pageHeight,
            IReadOnlyDictionary<string, int> slideImages
        )
        {
            if (shape.Image == null || string.IsNullOrEmpty(shape.Image.PartUri)) return;

            var x = shape.X.Value * EmuToPoints;
            var y = shape.Y.Value * EmuToPoints;
            var w = shape.Width.Value * EmuToPoints;
            var h = shape.Height.Value * EmuToPoints;
            var pdfY = pageHeight - y - h;

            var name = XObjectName(shape.Image.PartUri);
            if (!slideImages.ContainsKey(name)) return;

            AppendLine(sb, "q");
            AppendLine(sb, $"{w:F4} 0 0 {h:F4} {x:F4} {pdfY:F4} cm");
            AppendLine(sb, $"/{name} Do");
            AppendLine(sb, "Q");
        }

        private static void WriteTextFrame(
            StringBuilder sb,
            TextFrame frame,
            double shapeX,
            double shapeY,
            double shapeH,
            double pageHeight,
            ColorScheme? colorScheme,
            ColorSpec? styleTextColor,
            IReadOnlyDictionary<string, (int FileObj, int DescObj, int FontObj)> fontObjNums,
            IReadOnlyDictionary<string, string> fontKeys
        )
        {
            var paragraphs = frame.Paragraphs;
            if (paragraphs.Count == 0) return;

            const double marginPt = TextConstants.MinTextInset;
            var cursorY = shapeY + marginPt;
            const double defaultFontSize = TextConstants.DefaultFontSizePt;
            const double lineHeightFactor = TextConstants.DefaultLineHeightFactor;

            // Default text color
            (double Dr, double Dg, double Db) defaultRgb;
            if (styleTextColor.HasValue)
                defaultRgb = ToRgbF(styleTextColor.Value.Resolve(colorScheme));
            else if (colorScheme is not null)
                defaultRgb = ToRgbF(colorScheme.Dark1.Resolve(colorScheme));
            else
                defaultRgb = (0, 0, 0);

            foreach (var para in paragraphs)
            {
                if (para.Runs.Count == 0)
                {
                    cursorY += defaultFontSize * lineHeightFactor;
                    continue;
                }

                var fontSize = para.Runs
                    .Select(static r => r.Format.FontSizePoints ?? defaultFontSize)
                    .DefaultIfEmpty(defaultFontSize)
                    .Max();

                var lineH = fontSize * lineHeightFactor;
                var baselineY = cursorY + fontSize;

                if (baselineY > shapeY + shapeH - marginPt) break;

                var pdfBaselineY = pageHeight - baselineY;

                AppendLine(sb, "BT");
                // Initial font selection: first run's font or fallback.
                var firstRun = para.Runs.FirstOrDefault(static r => !string.IsNullOrEmpty(r.Text));
                var initPdfFont = firstRun is not null
                    ? ResolvePdfFontRef(firstRun.Format, fontObjNums, fontKeys)
                    : "/Fhv";
                AppendLine(sb, $"{initPdfFont} {fontSize:F4} Tf");
                AppendLine(sb, $"{defaultRgb.Dr:F4} {defaultRgb.Dg:F4} {defaultRgb.Db:F4} rg");

                var textX = shapeX + marginPt;
                var textSet = false;
                var currentFontRef = initPdfFont;

                foreach (var run in para.Runs.Where(static r => !string.IsNullOrEmpty(r.Text)))
                {
                    var runFontSize = run.Format.FontSizePoints ?? fontSize;
                    var runFontRef = ResolvePdfFontRef(
                        run.Format,
                        fontObjNums,
                        fontKeys
                    );

                    if (!textSet)
                    {
                        AppendLine(sb, $"{textX:F4} {pdfBaselineY:F4} Td");
                        textSet = true;
                    }

                    // Switch font if needed.
                    if (runFontRef != currentFontRef || Math.Abs(runFontSize - fontSize) > 0.1)
                    {
                        AppendLine(sb, $"{runFontRef} {runFontSize:F4} Tf");
                        currentFontRef = runFontRef;
                    }

                    // Set text color.
                    if (run.Format.Fill?.Solid != null)
                    {
                        var (r, g, b) = ToRgbF(run.Format.Fill.Solid.Color.Resolve(colorScheme));
                        AppendLine(sb, $"{r:F4} {g:F4} {b:F4} rg");
                    }

                    AppendLine(sb, $"({EscapePdfString(run.Text)}) Tj");
                }

                AppendLine(sb, "ET");
                cursorY += lineH;
            }
        }

        // Returns the PDF font reference string (e.g. "/F0" or "/Fhv") for a run.
        private static string ResolvePdfFontRef(
            RunFormat format,
            IReadOnlyDictionary<string, (int FileObj, int DescObj, int FontObj)> fontObjNums,
            IReadOnlyDictionary<string, string> fontKeys
        )
        {
            var typeface = format.LatinFont ?? TextConstants.FallbackLatinFont;
            var style = ResolveStyle(format);
            var key = $"{typeface}|{style}";
            return fontKeys.TryGetValue(key, out var resourceName) && fontObjNums.ContainsKey(key)
                ? $"/{resourceName}"
                : "/Fhv";
        }

        private static EmbeddedFontStyle ResolveStyle(RunFormat format)
        {
            var bold = format.Bold == InheritableBool.True;
            var italic = format.Italic == InheritableBool.True;
            return (bold, italic) switch
            {
                (true, true) => EmbeddedFontStyle.BoldItalic,
                (true, false) => EmbeddedFontStyle.Bold,
                (false, true) => EmbeddedFontStyle.Italic,
                _ => EmbeddedFontStyle.Regular
            };
        }

        // ── Image collection & writing ────────────────────────────────────────

        private static void CollectImages(Slide slide, Dictionary<string, int> imageMap)
        {
            foreach (var shape in slide.Shapes.OfType<PictureShape>()
                         .Where(static shape => shape.Image != null && !string.IsNullOrEmpty(shape.Image.PartUri)))
                imageMap.TryAdd(shape.Image!.PartUri, 0);
        }

        private static Dictionary<string, int> CollectSlideImages(
            Slide slide,
            IReadOnlyDictionary<string, int> imageObjNums
        )
        {
            var result = new Dictionary<string, int>();
            foreach (var shape in slide.Shapes.OfType<PictureShape>()
                         .Where(static shape => shape.Image != null && !string.IsNullOrEmpty(shape.Image.PartUri)))
            {
                var name = XObjectName(shape.Image!.PartUri);
                if (imageObjNums.TryGetValue(shape.Image.PartUri, out var num))
                    result[name] = num;
            }

            return result;
        }

        private void WriteSlideImages(
            Slide slide,
            IReadOnlyDictionary<string, int> imageObjNums
        )
        {
            var written = new HashSet<string>();
            foreach (var shape in slide.Shapes.OfType<PictureShape>()
                         .Where(static shape => shape.Image != null && !string.IsNullOrEmpty(shape.Image.PartUri))
                         .Where(shape => written.Add(shape.Image!.PartUri)))
            {
                if (!imageObjNums.TryGetValue(shape.Image!.PartUri, out var objNum))
                    continue;

                WriteImageXObject(
                    objNum,
                    shape.Image.Data,
                    shape.Image.ContentType,
                    shape.Image.PixelWidth,
                    shape.Image.PixelHeight
                );
            }
        }

        private void WriteImageXObject(
            int objNum,
            ReadOnlyMemory<byte> data,
            string contentType,
            int pixelWidth,
            int pixelHeight
        )
        {
            var isJpeg = contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase)
                         || contentType.Contains("jpg", StringComparison.OrdinalIgnoreCase);

            if (!isJpeg)
            {
                WriteWhiteImageXObject(objNum, Math.Max(1, pixelWidth), Math.Max(1, pixelHeight));
                return;
            }

            var imageData = data.ToArray();
            StartObj(objNum);
            WriteLn("<< /Type /XObject /Subtype /Image");
            WriteLn($"   /Width {Math.Max(1, pixelWidth)} /Height {Math.Max(1, pixelHeight)}");
            WriteLn("   /ColorSpace /DeviceRGB /BitsPerComponent 8");
            WriteLn("   /Filter /DCTDecode");
            WriteLn($"   /Length {imageData.Length} >>");
            WriteLn("stream");
            WriteBytes(imageData);
            WriteLn("\nendstream");
            EndObj();
        }

        private void WriteWhiteImageXObject(int objNum, int w, int h)
        {
            var rawBytes = Enumerable.Repeat((byte)0xFF, w * h * 3).ToArray();
            StartObj(objNum);
            WriteLn("<< /Type /XObject /Subtype /Image");
            WriteLn($"   /Width {w} /Height {h}");
            WriteLn("   /ColorSpace /DeviceRGB /BitsPerComponent 8");
            WriteLn($"   /Length {rawBytes.Length} >>");
            WriteLn("stream");
            WriteBytes(rawBytes);
            WriteLn("\nendstream");
            EndObj();
        }

        // ── Structure tree (tagged PDF) ───────────────────────────────────────

        // Writes the /StructTreeRoot and per-shape structure elements.
        // Each non-decorative shape gets one /StructElem with /MCID reference.
        private void WriteStructTree(
            int structTreeRootNum,
            IReadOnlyList<Slide> slides,
            IReadOnlyList<int> pageNums,
            IReadOnlyList<List<int>> structElemNums
        )
        {
            // Collect all struct element obj numbers flat.
            var allElems = new List<(int ObjNum, int PageObj, string Type, string? AltText)>();
            for (var pi = 0; pi < slides.Count; pi++)
            {
                var slide = slides[pi];
                var pageObj = pageNums[pi];
                var elems = structElemNums[pi];
                var elemIdx = 0;
                foreach (var shape in slide.Shapes.Where(static shape => !shape.IsDecorative).TakeWhile(_ => elemIdx < elems.Count))
                {
                    allElems.Add(
                        (elems[elemIdx], pageObj,
                            StructTypeForShape(shape),
                            shape is PictureShape ? (shape.AltText ?? string.Empty) : null)
                    );
                    elemIdx++;
                }
            }

            // StructTreeRoot: references all top-level struct elements.
            StartObj(structTreeRootNum);
            Write("<< /Type /StructTreeRoot /Kids [");
            foreach (var (objNum, _, _, _) in allElems) Write($" {objNum} 0 R");
            WriteLn(" ] >>");
            EndObj();

            // Individual struct elements.
            for (var i = 0; i < allElems.Count; i++)
            {
                var (objNum, pageObj, structType, altText) = allElems[i];
                StartObj(objNum);
                Write($"<< /Type /StructElem /S /{structType}");
                Write($" /P {structTreeRootNum} 0 R");
                Write($" /Pg {pageObj} 0 R");
                Write($" /K {i}"); // MCID
                if (!string.IsNullOrEmpty(altText))
                    Write($" /Alt ({EscapePdfString(altText)})");
                WriteLn(" >>");
                EndObj();
            }
        }

        // ── PDF object helpers ────────────────────────────────────────────────

        private int AllocObj()
        {
            _offsets.Add(0);
            return _nextObjNum++;
        }

        private void StartObj(int num)
        {
            _offsets[num - 1] = _buf.Length;
            WriteRaw($"{num} 0 obj\n");
        }

        private void EndObj() => WriteRaw("endobj\n");

        private void WriteLn(string s) => WriteRaw(s + "\n");
        private void Write(string s) => WriteRaw(s);

        private void WriteRaw(string s) =>
            _buf.Write(Encoding.Latin1.GetBytes(s));

        private void WriteBytes(byte[] bytes) => _buf.Write(bytes);

        // ── Text helpers ──────────────────────────────────────────────────────

        private static string EscapePdfString(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text.Where(static c => c <= 126 && c >= 32))
            {
                switch (c)
                {
                    case '(': sb.Append(@"\("); break;
                    case ')': sb.Append(@"\)"); break;
                    case '\\': sb.Append(@"\\"); break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }

        private static void AppendLine(StringBuilder sb, string line) =>
            sb.Append(line).Append('\n');

        private static (double r, double g, double b) ToRgbF(uint argb)
        {
            var (_, r, g, b) = ColorMath.UnpackArgb(argb);
            return (r / 255.0, g / 255.0, b / 255.0);
        }

        private static string XObjectName(string partUri) =>
            "Im" + Math.Abs(partUri.GetHashCode());

        // Sanitizes a font name for use as a PDF name (removes spaces and special chars).
        private static string SanitizePdfName(string name) => new(name.Where(static c => c > 32 && c != '/' && c != '#').ToArray());
    }
}
