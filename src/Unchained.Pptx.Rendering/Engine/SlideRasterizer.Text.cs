using Unchained.Drawing;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Drawing.Text;
using Unchained.Drawing.Text.Extensions;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Shapes;
using Buffer = HarfBuzzSharp.Buffer;

namespace Unchained.Pptx.Rendering.Engine;

// Text-frame layout (margins, columns, vertical anchor, auto-fit, word-wrap), text
// measurement, theme/embedded font resolution, and HarfBuzz/FreeType glyph blitting.
internal sealed partial class SlideRasterizer
{
    // Renders a single line of text at a fixed size/colour (used by chart titles + SmartArt nodes).
    private void RenderTextFrameText(
        RasterBuffer buffer,
        string text,
        int x,
        int y,
        int maxW,
        double sizePt,
        double dpi,
        byte r,
        byte g,
        byte b
    )
    {
        var scale = dpi / RenderingConstants.PointsPerInch;
        var lineHeight = 0;
        RenderRunText(
            buffer,
            text,
            TextConstants.FallbackLatinFont,
            null,
            sizePt,
            scale,
            x,
            y,
            x + maxW,
            r,
            g,
            b,
            ref lineHeight
        );
    }

    private void RenderTextFrame(
        RasterBuffer buffer,
        TextFrame textFrame,
        int shapeX,
        int shapeY,
        int shapeWidth,
        int shapeHeight,
        double dpi,
        ColorScheme? colorScheme = null,
        ColorSpec? styleTextColor = null,
        PlaceholderType placeholderType = PlaceholderType.None,
        FontScheme? fontScheme = null
    )
    {
        if (textFrame.Paragraphs.Count == 0)
            return;

        var scale = dpi / RenderingConstants.PointsPerInch;
        var marginLeft = (int)(textFrame.Format.MarginLeft.ToPoints() * scale / dpi * 96);
        var marginTop = (int)(textFrame.Format.MarginTop.ToPoints() * scale / dpi * 96);
        var marginBottom = (int)(textFrame.Format.MarginBottom.ToPoints() * scale / dpi * 96);

        // Multi-column layout: distribute paragraphs evenly across columns.
        var colCount = Math.Max(1, textFrame.Format.ColumnCount);
        if (colCount > 1)
        {
            var spacingPx = (int)(textFrame.Format.ColumnSpacing.ToPoints() * scale / dpi * 96);
            var colW = (shapeWidth - ((colCount - 1) * spacingPx)) / colCount;
            if (colW > 0)
            {
                // Evenly distribute paragraphs across columns.
                var parasPerCol = (int)Math.Ceiling((double)textFrame.Paragraphs.Count / colCount);
                for (var col = 0; col < colCount; col++)
                {
                    var colX = shapeX + (col * (colW + spacingPx));
                    var start = col * parasPerCol;
                    var end = Math.Min(start + parasPerCol, textFrame.Paragraphs.Count);
                    if (start >= end) break;

                    // Build a temporary TextFrame with just this column's paragraphs.
                    var colFrame = new TextFrame
                    {
                        Format =
                        {
                            VerticalAnchor = textFrame.Format.VerticalAnchor,
                            Autofit = textFrame.Format.Autofit,
                            MarginLeft = textFrame.Format.MarginLeft,
                            MarginTop = textFrame.Format.MarginTop,
                            MarginBottom = textFrame.Format.MarginBottom,
                            MarginRight = textFrame.Format.MarginRight
                        }
                    };
                    for (var pi = start; pi < end; pi++)
                        colFrame.Paragraphs.Add(textFrame.Paragraphs[pi]);

                    RenderTextFrame(
                        buffer,
                        colFrame,
                        colX,
                        shapeY,
                        colW,
                        shapeHeight,
                        dpi,
                        colorScheme,
                        styleTextColor,
                        placeholderType,
                        fontScheme
                    );
                }

                return;
            }
        }

        var innerLeft = shapeX + Math.Max(4, marginLeft);
        var maxX = shapeX + shapeWidth - 4;
        var maxY = shapeY + shapeHeight - 2;

        // Default font size based on placeholder type when run has no explicit size.
        var defaultFontSize = placeholderType switch
        {
            PlaceholderType.Title => 36.0,
            PlaceholderType.CenteredTitle => 36.0,
            PlaceholderType.Subtitle => 24.0,
            PlaceholderType.Body => 18.0,
            _ => TextConstants.DefaultFontSizePt
        };

        // Default text color priority: styleTextColor → theme dk1 → black.
        uint defaultTextArgb;
        if (styleTextColor.HasValue)
            defaultTextArgb = styleTextColor.Value.Resolve(colorScheme);
        else if (colorScheme is not null)
            defaultTextArgb = colorScheme.Dark1.Resolve(colorScheme);
        else
            defaultTextArgb = 0xFF000000u;
        ExtractArgb(defaultTextArgb, out _, out var defaultR, out var defaultG, out var defaultB);

        // Measure total text height for vertical anchor (Middle/Bottom).
        var anchor = textFrame.Format.VerticalAnchor;
        var startY = shapeY + Math.Max(4, marginTop);
        if (anchor is TextAnchor.Middle
            or TextAnchor.Bottom
            or TextAnchor.MiddleCentered
            or TextAnchor.BottomCentered)
        {
            var totalTextH = MeasureTotalTextHeight(
                textFrame,
                scale,
                defaultFontSize
            );
            var availH = shapeHeight - marginTop - marginBottom;
            var offset = availH - totalTextH;
            if (anchor is TextAnchor.Middle
                or TextAnchor.MiddleCentered)
                startY = shapeY + marginTop + (int)(offset / 2.0);
            else
                startY = shapeY + marginTop + offset;
            startY = Math.Max(shapeY + 2, startY);
        }

        // Auto-fit: if ShrinkText, find the largest scale that makes all text fit.
        var fontScale = 1.0;
        if (textFrame.Format.Autofit == TextAutofit.ShrinkText)
        {
            var availH = shapeHeight - Math.Max(4, marginTop) - Math.Max(2, marginBottom);
            if (availH > 0)
            {
                // Binary search: scale between 0.1 and 1.0
                var lo = 0.1;
                var hi = 1.0;
                for (var iter = 0; iter < 8; iter++)
                {
                    var mid = (lo + hi) / 2;
                    var h = MeasureTotalTextHeight(textFrame, scale * mid, defaultFontSize * mid);
                    if (h <= availH) lo = mid;
                    else hi = mid;
                }

                fontScale = lo;
            }
        }

        var cursorY = startY;

        foreach (var paragraph in textFrame.Paragraphs)
        {
            if (paragraph.Runs.Count == 0)
            {
                cursorY += (int)(defaultFontSize * fontScale * scale) + 2;
                if (cursorY > maxY) return;

                continue;
            }

            // Collect word tokens for word-wrap.
            var tokens = new List<(string Word, string FontName, byte[]? EmbBytes, double SizePt, byte R, byte G, byte B)>();
            foreach (var run in paragraph.Runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;

                var fontSizePt = (run.Format.FontSizePoints ?? defaultFontSize) * fontScale;
                byte textR, textG, textB;
                if (run.Format.Fill is { Type: FillType.Solid, Solid: not null })
                {
                    var argb = run.Format.Fill.Solid.Color.Resolve(colorScheme);
                    ExtractArgb(argb, out _, out textR, out textG, out textB);
                }
                else
                {
                    textR = defaultR;
                    textG = defaultG;
                    textB = defaultB;
                }

                var fontName = ResolveFont(run.Format.LatinFont ?? SelectFontName(run), fontScheme);
                var embeddedBytes = ResolveEmbeddedFont(run, fontName, out var cacheKey);

                tokens.AddRange(SplitIntoWords(run.Text).Select(word => (word, cacheKey, embeddedBytes, fontSizePt, textR, textG, textB)));
            }

            var lineX = innerLeft;
            var lineHeight = 0;

            foreach (var (word, fontName, embBytes, sizePt, r, g, b) in tokens)
            {
                var pixelSize = (uint)Math.Max(1, Math.Round(sizePt * scale));
                var wordWidth = MeasureTextWidth(word, fontName, embBytes, pixelSize);

                if (lineX + wordWidth > maxX && lineX > innerLeft)
                {
                    cursorY += lineHeight + 2;
                    lineX = innerLeft;
                    lineHeight = 0;
                    if (cursorY > maxY) break;
                }

                var renderWord = lineX == innerLeft ? word.TrimStart() : word;
                if (string.IsNullOrEmpty(renderWord)) continue;

                var dummy = 0;
                lineX = RenderRunText(
                    buffer,
                    renderWord,
                    fontName,
                    embBytes,
                    sizePt,
                    scale,
                    lineX,
                    cursorY,
                    maxX,
                    r,
                    g,
                    b,
                    ref dummy
                );
                if (dummy > lineHeight) lineHeight = dummy;
            }

            cursorY += lineHeight + 2;
            if (cursorY > maxY) return;
        }
    }

    // Resolves +mj-lt / +mn-lt theme font references to real font family names.
    private static string ResolveFont(string fontName, FontScheme? fontScheme) =>
        fontScheme is null
            ? fontName
            : fontName switch
            {
                OoxmlScaling.ThemeMajorLatinFont => fontScheme.MajorFont.LatinFont is { Length: > 0 } mj ? mj : fontName,
                OoxmlScaling.ThemeMinorLatinFont => fontScheme.MinorFont.LatinFont is { Length: > 0 } mn ? mn : fontName,
                _ => fontName
            };

    // Estimates the total pixel height of all paragraphs in a text frame for vertical anchor.
    private static int MeasureTotalTextHeight(
        TextFrame textFrame,
        double scale,
        double defaultFontSize
    )
    {
        var total = 0;
        foreach (var para in textFrame.Paragraphs)
        {
            if (para.Runs.Count == 0)
            {
                total += (int)(defaultFontSize * scale) + 2;
                continue;
            }

            var maxSize = para.Runs.Max(r => r.Format.FontSizePoints ?? defaultFontSize);
            total += (int)(maxSize * scale) + 2;
        }

        return total;
    }

    // Splits text into words, keeping trailing spaces attached to each word.
    private static IEnumerable<string> SplitIntoWords(string text)
    {
        var result = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var start = i;
            while (i < text.Length && text[i] != ' ') i++;
            while (i < text.Length && text[i] == ' ') i++;
            if (i > start)
                result.Add(text[start..i]);
        }

        return result;
    }

    // Measures the pixel width of text using HarfBuzz advances.
    private int MeasureTextWidth(
        string text,
        string fontName,
        byte[]? embeddedBytes,
        uint pixelSize
    )
    {
        if (string.IsNullOrEmpty(text)) return 0;

        try
        {
            var (ftFace, hbFont) = fonts.GetFonts(fontName, embeddedBytes);
            ftFace.SetPixelSize(pixelSize);
            var hbScale = (int)(pixelSize * TextShapingConstants.HarfBuzzFixed);
            hbFont.SetScale(hbScale, hbScale);

            using var hbBuffer = new Buffer();
            hbBuffer.AddUtf8(text.ToUtf8Span());
            hbBuffer.GuessSegmentProperties();
            hbFont.Shape(hbBuffer);

            return hbBuffer.GlyphPositions.Sum(static p => p.XAdvance) / TextShapingConstants.HarfBuzzFixed;
        }
        catch
        {
            return (int)(text.Length * pixelSize * 0.6);
        }
    }

    private int RenderRunText(
        RasterBuffer buffer,
        string text,
        string fontName,
        byte[]? embeddedBytes,
        double fontSizePt,
        double scale,
        int startX,
        int startY,
        int maxX,
        byte r,
        byte g,
        byte b,
        ref int lineHeight
    )
    {
        var cursorX = startX;

        // ReSharper disable once EmptyGeneralCatchClause
        try
        {
            var (ftFace, hbFont) = fonts.GetFonts(fontName, embeddedBytes);
            var pixelSize = (uint)Math.Max(1, Math.Round(fontSizePt * scale));
            ftFace.SetPixelSize(pixelSize);

            if (lineHeight < (int)pixelSize)
                lineHeight = (int)pixelSize;

            var hbScale = (int)(pixelSize * TextShapingConstants.HarfBuzzFixed);
            hbFont.SetScale(hbScale, hbScale);

            using var hbBuffer = new Buffer();
            hbBuffer.AddUtf8(text.ToUtf8Span());
            hbBuffer.GuessSegmentProperties();
            hbFont.Shape(hbBuffer);

            var glyphInfos = hbBuffer.GlyphInfos;
            var glyphPositions = hbBuffer.GlyphPositions;

            for (var i = 0; i < glyphInfos.Length; i++)
            {
                var glyphId = glyphInfos[i].Codepoint;

                if (!ftFace.TryLoadGlyph(glyphId))
                    continue;

                var penX = cursorX + (glyphPositions[i].XOffset / TextShapingConstants.HarfBuzzFixed);
                var penY = startY + (int)pixelSize + (glyphPositions[i].YOffset / TextShapingConstants.HarfBuzzFixed);

                buffer.BlitGlyphFromFace(
                    penX,
                    penY,
                    ftFace,
                    r,
                    g,
                    b
                );

                cursorX += glyphPositions[i].XAdvance / TextShapingConstants.HarfBuzzFixed;

                if (cursorX >= maxX)
                    break;
            }
        }
        catch { }

        return cursorX;
    }

    private static string SelectFontName(Run run) =>
        run.Format.Bold == InheritableBool.True ? TextConstants.FallbackLatinFontBold : TextConstants.FallbackLatinFont;

    private byte[]? ResolveEmbeddedFont(Run run, string fontName, out string cacheKey)
    {
        cacheKey = fontName;
        if (media is null || media.Fonts.Count == 0)
            return null;

        var style = ResolveStyle(run);
        var data = media.FindFontData(fontName, style);
        if (data is null)
            return null;

        cacheKey = $"{fontName}#{style}#embedded";
        return data.Value.ToArray();
    }

    private static EmbeddedFontStyle ResolveStyle(Run run)
    {
        var bold = run.Format.Bold == InheritableBool.True;
        var italic = run.Format.Italic == InheritableBool.True;
        return (bold, italic) switch
        {
            (true, true) => EmbeddedFontStyle.BoldItalic,
            (true, false) => EmbeddedFontStyle.Bold,
            (false, true) => EmbeddedFontStyle.Italic,
            _ => EmbeddedFontStyle.Regular
        };
    }
}
