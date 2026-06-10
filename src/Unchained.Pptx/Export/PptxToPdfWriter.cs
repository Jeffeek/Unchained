using System.Text;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Ooxml.Text;

namespace Unchained.Pptx.Export;

/// <summary>
/// Converts a <see cref="PresentationDocument"/> into a PDF 1.7 byte stream.
/// Each non-hidden slide becomes one PDF page at the correct dimensions.
/// Text is embedded as selectable PDF text using the standard Helvetica font.
/// Images are embedded as PDF image XObjects.
/// </summary>
internal static class PptxToPdfWriter
{
    // EMU → points: 1 pt = 12700 EMU
    private const double EmuToPoints = 1.0 / 12700.0;

    /// <summary>
    /// Generates a PDF from <paramref name="document"/> and returns the raw bytes.
    /// </summary>
    public static byte[] Write(PresentationDocument document, PdfSaveOptions options)
    {
        var slides = CollectSlides(document.Slides, options);
        var writer = new PdfBuilder();
        writer.WritePdf(slides, document.SlideSize, options);
        return writer.ToArray();
    }

    private static List<Slide> CollectSlides(SlideCollection slides, PdfSaveOptions options)
    {
        var result = new List<Slide>(slides.Count);
        for (var i = 0; i < slides.Count; i++)
        {
            var slide = slides[i];
            if (slide.IsHidden && !options.IncludeHiddenSlides) continue;
            result.Add(slide);
        }
        return result;
    }

    // ── PDF builder ────────────────────────────────────────────────────────────

    private sealed class PdfBuilder
    {
        private readonly MemoryStream _buf = new();
        private readonly List<long> _offsets = [];
        private int _nextObjNum = 1;

        public byte[] ToArray() => _buf.ToArray();

        public void WritePdf(List<Slide> slides, SlideSize slideSize, PdfSaveOptions options)
        {
            WriteRaw("%PDF-1.7\n%\xC7\xEC\x8F\xA2\n");

            var catalogNum = AllocObj();
            var pagesNum = AllocObj();

            // Pre-allocate page / content / image object numbers
            var pageNums = slides.Select(_ => AllocObj()).ToList();
            var contentNums = slides.Select(_ => AllocObj()).ToList();

            // Collect images across all slides
            var imageMap = new Dictionary<string, int>(); // PartUri → obj number
            foreach (var slide in slides)
                CollectImages(slide, imageMap);

            // Allocate image object numbers
            var imageObjNums = imageMap.Keys
                .ToDictionary(k => k, _ => AllocObj());

            var fontNum = AllocObj();

            // Write page + content objects
            for (var i = 0; i < slides.Count; i++)
            {
                options.Progress?.Report((double)i / slides.Count * 0.8);

                var slide = slides[i];
                var widthPt = slideSize.Width.Value * EmuToPoints;
                var heightPt = slideSize.Height.Value * EmuToPoints;

                // Collect images referenced by this slide
                var slideImages = CollectSlideImages(slide, imageMap, imageObjNums);

                // Build content stream
                var contentBytes = BuildContentStream(
                    slide, widthPt, heightPt, fontNum, slideImages);

                // Write page object
                StartObj(pageNums[i]);
                WriteLn($"<< /Type /Page /Parent {pagesNum} 0 R");
                WriteLn($"   /MediaBox [0 0 {widthPt:F4} {heightPt:F4}]");
                WriteLn($"   /Contents {contentNums[i]} 0 R");
                Write("   /Resources << /Font << /F1 ");
                WriteLn($"{fontNum} 0 R >> ");
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
                WriteSlideImages(slide, imageMap, imageObjNums);

            // Write Helvetica font
            StartObj(fontNum);
            WriteLn("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica");
            WriteLn("   /Encoding /WinAnsiEncoding >>");
            EndObj();

            // Write Pages tree
            StartObj(pagesNum);
            Write($"<< /Type /Pages /Count {slides.Count} /Kids [");
            foreach (var n in pageNums) Write($" {n} 0 R");
            WriteLn(" ] >>");
            EndObj();

            // Write Catalog
            StartObj(catalogNum);
            WriteLn($"<< /Type /Catalog /Pages {pagesNum} 0 R >>");
            EndObj();

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

        // ── Content stream ────────────────────────────────────────────────────

        private static byte[] BuildContentStream(
            Slide slide, double pageWidth, double pageHeight,
            int fontObjNum, Dictionary<string, int> slideImages)
        {
            var sb = new StringBuilder();
            var colorScheme = slide.Master?.Theme?.Colors;

            // White background
            AppendLine(sb, "q");
            AppendLine(sb, "1 1 1 rg");
            AppendLine(sb, $"0 0 {pageWidth:F4} {pageHeight:F4} re f");
            AppendLine(sb, "Q");

            // Slide background fill
            WriteBackground(sb, slide, pageWidth, pageHeight, colorScheme);

            // Shapes (bottom-to-top Z-order = insertion order)
            foreach (var shape in slide.Shapes)
            {
                switch (shape)
                {
                    case AutoShape auto:
                        WriteAutoShape(sb, auto, pageHeight, colorScheme);
                        break;
                    case PictureShape pic:
                        WritePictureShape(sb, pic, pageHeight, slideImages);
                        break;
                }
            }

            return Encoding.Latin1.GetBytes(sb.ToString());
        }

        private static void WriteBackground(
            StringBuilder sb, Slide slide, double pageWidth, double pageHeight,
            Unchained.Ooxml.Drawing.ColorScheme? colorScheme)
        {
            // Walk slide → layout → master for background fill.
            Unchained.Ooxml.Drawing.FillFormat? fill = null;
            if (slide.Background.Fill.Type != Unchained.Ooxml.Drawing.FillType.None)
                fill = slide.Background.Fill;
            else if (slide.Layout?.Background.Fill.Type != Unchained.Ooxml.Drawing.FillType.None)
                fill = slide.Layout!.Background.Fill;
            else if (slide.Master?.Background.Fill.Type != Unchained.Ooxml.Drawing.FillType.None)
                fill = slide.Master!.Background.Fill;

            if (fill is null || fill.Type != Unchained.Ooxml.Drawing.FillType.Solid || fill.Solid == null)
                return;

            var (r, g, b) = ToRgbF(fill.Solid.Color.Resolve(colorScheme));
            AppendLine(sb, "q");
            AppendLine(sb, $"{r:F4} {g:F4} {b:F4} rg");
            AppendLine(sb, $"0 0 {pageWidth:F4} {pageHeight:F4} re f");
            AppendLine(sb, "Q");
        }

        private static void WriteAutoShape(
            StringBuilder sb, AutoShape shape, double pageHeight,
            Unchained.Ooxml.Drawing.ColorScheme? colorScheme)
        {
            var x = shape.X.Value * EmuToPoints;
            var y = shape.Y.Value * EmuToPoints;
            var w = shape.Width.Value * EmuToPoints;
            var h = shape.Height.Value * EmuToPoints;

            // PDF Y-axis is inverted: flip from top-left to bottom-left
            var pdfY = pageHeight - y - h;

            AppendLine(sb, "q");

            // Fill — spPr solid → style fill → noFill.
            if (shape.Fill.Type == FillType.Solid && shape.Fill.Solid != null)
            {
                var (r, g, b) = ToRgbF(shape.Fill.Solid.Color.Resolve(colorScheme));
                AppendLine(sb, $"{r:F4} {g:F4} {b:F4} rg");
                AppendLine(sb, $"{x:F4} {pdfY:F4} {w:F4} {h:F4} re f");
            }
            else if (shape.Fill.Type == FillType.None && shape.StyleFillColor.HasValue)
            {
                var (r, g, b) = ToRgbF(shape.StyleFillColor.Value.Resolve(colorScheme));
                AppendLine(sb, $"{r:F4} {g:F4} {b:F4} rg");
                AppendLine(sb, $"{x:F4} {pdfY:F4} {w:F4} {h:F4} re f");
            }
            else if (shape.Fill.Type == FillType.None)
            {
                // No fill — only border/text
            }
            else
            {
                // Default light grey for unrecognised fills
                AppendLine(sb, "0.95 0.95 0.95 rg");
                AppendLine(sb, $"{x:F4} {pdfY:F4} {w:F4} {h:F4} re f");
            }

            // Stroke
            if (shape.Line.Fill.Type == FillType.Solid && shape.Line.Fill.Solid != null)
            {
                var (r, g, b) = ToRgbF(shape.Line.Fill.Solid.Color.Resolve(colorScheme));
                var lw = shape.Line.WidthPoints ?? 0.75;
                AppendLine(sb, $"{r:F4} {g:F4} {b:F4} RG");
                AppendLine(sb, $"{lw:F4} w");
                AppendLine(sb, $"{x:F4} {pdfY:F4} {w:F4} {h:F4} re S");
            }

            AppendLine(sb, "Q");

            // Text
            WriteTextFrame(sb, shape.TextFrame, x, y, w, h, pageHeight, colorScheme, shape.StyleTextColor);
        }

        private static void WritePictureShape(
            StringBuilder sb, PictureShape shape, double pageHeight,
            Dictionary<string, int> slideImages)
        {
            if (shape.Image == null || string.IsNullOrEmpty(shape.Image.PartUri)) return;

            var x = shape.X.Value * EmuToPoints;
            var y = shape.Y.Value * EmuToPoints;
            var w = shape.Width.Value * EmuToPoints;
            var h = shape.Height.Value * EmuToPoints;
            var pdfY = pageHeight - y - h;

            var xobjName = XObjectName(shape.Image.PartUri);
            if (!slideImages.ContainsKey(xobjName)) return;

            AppendLine(sb, "q");
            AppendLine(sb, $"{w:F4} 0 0 {h:F4} {x:F4} {pdfY:F4} cm");
            AppendLine(sb, $"/{xobjName} Do");
            AppendLine(sb, "Q");
        }

        private static void WriteTextFrame(
            StringBuilder sb, TextFrame frame,
            double shapeX, double shapeY, double shapeW, double shapeH,
            double pageHeight,
            Unchained.Ooxml.Drawing.ColorScheme? colorScheme = null,
            Unchained.Ooxml.Drawing.ColorSpec? styleTextColor = null)
        {
            var paragraphs = frame.Paragraphs;
            if (paragraphs.Count == 0) return;

            // Simple top-to-bottom text layout
            const double MarginPt = 4.0;
            var cursorY = shapeY + MarginPt;
            const double DefaultFontSize = 12.0;
            const double LineHeightFactor = 1.25;

            // Default text color: styleTextColor → dk1 → black.
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
                    cursorY += DefaultFontSize * LineHeightFactor;
                    continue;
                }

                var fontSize = para.Runs
                    .Select(static r => r.Format.FontSizePoints ?? DefaultFontSize)
                    .DefaultIfEmpty(DefaultFontSize)
                    .Max();

                var lineH = fontSize * LineHeightFactor;
                var baselineY = cursorY + fontSize;

                if (baselineY > shapeY + shapeH - MarginPt) break;

                var pdfBaselineY = pageHeight - baselineY;

                AppendLine(sb, "BT");
                AppendLine(sb, $"/F1 {fontSize:F4} Tf");
                AppendLine(sb, $"{defaultRgb.Dr:F4} {defaultRgb.Dg:F4} {defaultRgb.Db:F4} rg");

                var textX = shapeX + MarginPt;
                var textSet = false;

                foreach (var run in para.Runs)
                {
                    if (string.IsNullOrEmpty(run.Text)) continue;

                    var runFontSize = run.Format.FontSizePoints ?? fontSize;

                    if (!textSet)
                    {
                        AppendLine(sb, $"{textX:F4} {pdfBaselineY:F4} Td");
                        textSet = true;
                    }

                    // Set text color
                    if (run.Format.Fill?.Solid != null)
                    {
                        var (r, g, b) = ToRgbF(run.Format.Fill.Solid.Color.Resolve(colorScheme));
                        AppendLine(sb, $"{r:F4} {g:F4} {b:F4} rg");
                    }

                    // If font size differs, switch
                    if (Math.Abs(runFontSize - fontSize) > 0.1)
                        AppendLine(sb, $"/F1 {runFontSize:F4} Tf");

                    AppendLine(sb, $"({EscapePdfString(run.Text)}) Tj");
                }

                AppendLine(sb, "ET");
                cursorY += lineH;
            }
        }

        // ── Image collection & writing ────────────────────────────────────────

        private static void CollectImages(Slide slide, Dictionary<string, int> imageMap)
        {
            foreach (var shape in slide.Shapes.OfType<PictureShape>())
            {
                if (shape.Image == null || string.IsNullOrEmpty(shape.Image.PartUri)) continue;
                if (!imageMap.ContainsKey(shape.Image.PartUri))
                    imageMap[shape.Image.PartUri] = 0; // placeholder, real num assigned later
            }
        }

        private static Dictionary<string, int> CollectSlideImages(
            Slide slide,
            Dictionary<string, int> imageMap,
            Dictionary<string, int> imageObjNums)
        {
            var result = new Dictionary<string, int>();
            foreach (var shape in slide.Shapes.OfType<PictureShape>())
            {
                if (shape.Image == null || string.IsNullOrEmpty(shape.Image.PartUri)) continue;
                var name = XObjectName(shape.Image.PartUri);
                if (imageObjNums.TryGetValue(shape.Image.PartUri, out var num))
                    result[name] = num;
            }
            return result;
        }

        private void WriteSlideImages(
            Slide slide,
            Dictionary<string, int> imageMap,
            Dictionary<string, int> imageObjNums)
        {
            var written = new HashSet<string>();
            foreach (var shape in slide.Shapes.OfType<PictureShape>())
            {
                if (shape.Image == null || string.IsNullOrEmpty(shape.Image.PartUri)) continue;
                if (!written.Add(shape.Image.PartUri)) continue;
                if (!imageObjNums.TryGetValue(shape.Image.PartUri, out var objNum)) continue;

                WriteImageXObject(objNum, shape.Image.Data, shape.Image.ContentType,
                    shape.Image.PixelWidth, shape.Image.PixelHeight);
            }
        }

        private void WriteImageXObject(
            int objNum, ReadOnlyMemory<byte> data, string contentType,
            int pixelWidth, int pixelHeight)
        {
            var isJpeg = contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase)
                      || contentType.Contains("jpg", StringComparison.OrdinalIgnoreCase);

            if (!isJpeg)
            {
                // For non-JPEG: write a placeholder 1×1 white image
                // Full PNG decoding would require a decoder dependency not present here
                WriteWhiteImageXObject(objNum, Math.Max(1, pixelWidth), Math.Max(1, pixelHeight));
                return;
            }

            // JPEG: embed directly with /DCTDecode filter
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
            // 1×1 white pixel repeated h times — minimal valid image
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

        // ── PDF object helpers ────────────────────────────────────────────────

        private int AllocObj()
        {
            _offsets.Add(0); // placeholder
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
            foreach (var c in text)
            {
                if (c > 126 || c < 32) continue; // skip non-printable / non-Latin1 for now
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
            var r = ((argb >> 16) & 0xFF) / 255.0;
            var g = ((argb >> 8) & 0xFF) / 255.0;
            var b = (argb & 0xFF) / 255.0;
            return (r, g, b);
        }

        private static string XObjectName(string partUri) =>
            "Im" + Math.Abs(partUri.GetHashCode()).ToString();
    }
}
