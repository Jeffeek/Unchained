using System.Text;
using Unchained.Drawing.Primitives;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Export;

/// <summary>
///     Exports presentation slides to SVG — one SVG document per slide.
///     Shapes are rendered as SVG elements; text is embedded as
///     <c>&lt;text&gt;</c>; images are optionally inlined as Base64 data URIs.
/// </summary>
internal static class PptxToSvgWriter
{
    // EMU → SVG user units (points): 1 pt = 12700 EMU
    private const double EmuToPt = Emu.EmuToPoints;

    /// <summary>
    ///     Returns the SVG bytes for a single slide.
    /// </summary>
    public static byte[] WriteSlide(Slide slide, SlideSize slideSize, SvgSaveOptions options)
    {
        var w = slideSize.Width.Value * EmuToPt;
        var h = slideSize.Height.Value * EmuToPt;
        var colorScheme = slide.Master.Theme.Colors;
        var fontScheme = slide.Master.Theme.Fonts;

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");

        if (options.Responsive)
        {
            sb.AppendLine(
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                $"viewBox=\"0 0 {w:F4} {h:F4}\">"
            );
        }
        else
        {
            sb.AppendLine(
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                $"width=\"{w:F4}pt\" height=\"{h:F4}pt\" viewBox=\"0 0 {w:F4} {h:F4}\">"
            );
        }

        WriteBackground(sb, slide, w, h, colorScheme);

        // Shapes
        for (var i = 0; i < slide.Shapes.Count; i++)
            // ReSharper disable BadListLineBreaks
            WriteShape(sb, slide.Shapes[i], options, colorScheme, i, fontScheme, usedEmbeddedFonts: null);
        // ReSharper restore BadListLineBreaks

        sb.AppendLine("</svg>");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    ///     Returns the SVG bytes for a single slide, along with any font substitution warnings.
    /// </summary>
    public static (byte[] Svg, IReadOnlyList<string> FontWarnings) WriteSlide(
        Slide slide,
        SlideSize slideSize,
        SvgSaveOptions options,
        FontScheme fontScheme,
        IReadOnlyList<EmbeddedFont> embeddedFonts
    )
    {
        var w = slideSize.Width.Value * EmuToPt;
        var h = slideSize.Height.Value * EmuToPt;
        var colorScheme = slide.Master.Theme.Colors;

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");

        if (options.Responsive)
        {
            sb.AppendLine(
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                $"viewBox=\"0 0 {w:F4} {h:F4}\">"
            );
        }
        else
        {
            sb.AppendLine(
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                $"width=\"{w:F4}pt\" height=\"{h:F4}pt\" viewBox=\"0 0 {w:F4} {h:F4}\">"
            );
        }

        WriteBackground(sb, slide, w, h, colorScheme);

        var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Collect embedded fonts used by text runs.
        var usedEmbeddedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var slideShape in slide.Shapes)
        {
            switch (slideShape)
            {
                case AutoShape { TextFrame.Paragraphs.Count: > 0 } auto:
                {
                    foreach (var resolved in auto.TextFrame.Paragraphs
                                 .SelectMany(para => from run in para.Runs
                                                     select ResolveFontFamily(run.Format.LatinFont, fontScheme)
                                                     into resolved
                                                     let isEmbedded = embeddedFonts.Any(ef => ef.Typeface.Equals(resolved, StringComparison.OrdinalIgnoreCase))
                                                     where !isEmbedded
                                                     select resolved
                                 ))
                        unresolved.Add(resolved);
                    break;
                }
                case GroupShape grp:
                    CollectUnresolvedFonts(grp, fontScheme, embeddedFonts, unresolved);
                break;
            }
        }

        // Write embedded font @font-face rules.
        if (options.EmbedFonts && embeddedFonts.Count > 0)
        {
            sb.AppendLine("<defs><style>");
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var font in embeddedFonts)
            {
                if (written.Contains(font.Typeface)) continue;

                var base64 = Convert.ToBase64String(font.Data.Span);
                var family = EscapeCssIdentifier(font.Typeface);
                var styleAttr = font.Style switch
                {
                    EmbeddedFontStyle.BoldItalic => "font-weight:bold;font-style:italic",
                    EmbeddedFontStyle.Bold => "font-weight:bold",
                    EmbeddedFontStyle.Italic => "font-style:italic",
                    _ => string.Empty
                };
                sb.AppendLine($"@font-face {{font-family:\"{family}\";src:url(data:font/otf;base64,{base64}) format(\"opentype\"){(string.IsNullOrEmpty(styleAttr) ? "" : $";{styleAttr}")}}}");
                written.Add(font.Typeface);
            }

            sb.AppendLine("</style></defs>");
        }

        // Shapes
        for (var i = 0; i < slide.Shapes.Count; i++)
            // ReSharper disable BadListLineBreaks
            WriteShape(sb, slide.Shapes[i], options, colorScheme, i, fontScheme, usedEmbeddedFonts);
        // ReSharper restore BadListLineBreaks

        sb.AppendLine("</svg>");
        return (Encoding.UTF8.GetBytes(sb.ToString()), unresolved.ToList());
    }

    private static void CollectUnresolvedFonts(
        GroupShape group,
        FontScheme fontScheme,
        // ReSharper disable once ParameterTypeCanBeEnumerable.Local
        IReadOnlyCollection<EmbeddedFont> embeddedFonts,
        ISet<string> unresolved
    )
    {
        foreach (var child in group.Children)
        {
            switch (child)
            {
                case AutoShape { TextFrame.Paragraphs.Count: > 0 } auto:
                {
                    foreach (var resolved in auto.TextFrame.Paragraphs
                                 .SelectMany(para => from run in para.Runs
                                                     select ResolveFontFamily(run.Format.LatinFont, fontScheme)
                                                     into resolved
                                                     let isEmbedded = embeddedFonts.Any(ef => ef.Typeface.Equals(resolved, StringComparison.OrdinalIgnoreCase))
                                                     where !isEmbedded
                                                     select resolved
                                 ))
                        unresolved.Add(resolved);
                    break;
                }
                case GroupShape nested:
                    CollectUnresolvedFonts(nested, fontScheme, embeddedFonts, unresolved);
                break;
            }
        }
    }

    /// <summary>
    ///     Returns SVG bytes for every non-hidden slide in the document.
    /// </summary>
    public static byte[][] WriteAll(PresentationDocument document, SvgSaveOptions options)
    {
        var slides = document.Slides;
        var result = (from t in slides
                      where !t.IsHidden || options.IncludeHiddenSlides
                      select WriteSlide(t, document.SlideSize, options)).ToList();

        return [.. result];
    }

    private static void WriteBackground(
        StringBuilder sb,
        Slide slide,
        double w,
        double h,
        ColorScheme? colorScheme
    )
    {
        var fill = ResolveBackground(slide);
        if (fill is null || fill.Type != FillType.Solid || fill.Solid == null) return;

        var color = ToSvgColor(fill.Solid.Color.Resolve(colorScheme));
        sb.AppendLine($"<rect x=\"0\" y=\"0\" width=\"{w:F4}\" height=\"{h:F4}\" fill=\"{color}\"/>");
    }

    // Resolves background fill walking slide → layout → master.
    private static FillFormat? ResolveBackground(Slide slide) =>
        slide.Background.Fill.Type != FillType.None
            ? slide.Background.Fill
            : slide.Layout.Background.Fill.Type != FillType.None
                ? slide.Layout.Background.Fill
                : slide.Master.Background.Fill.Type != FillType.None
                    ? slide.Master.Background.Fill
                    : null;

    private static void WriteShape(
        StringBuilder sb,
        Shape shape,
        SvgSaveOptions options,
        ColorScheme? colorScheme,
        int index,
        FontScheme fontScheme,
        ISet<string>? usedEmbeddedFonts = null
    )
    {
        var x = shape.X.Value * EmuToPt;
        var y = shape.Y.Value * EmuToPt;
        var w = shape.Width.Value * EmuToPt;
        var h = shape.Height.Value * EmuToPt;

        // Resolve fill — spPr solid → style fill → noFill.
        var fillAttr = shape.Fill switch
        {
            { Type: FillType.Solid, Solid: not null } => $"fill=\"{ToSvgColor(shape.Fill.Solid.Color.Resolve(colorScheme))}\"",
            _ => shape.Fill.Type switch
            {
                FillType.None when shape.StyleFillColor.HasValue => $"fill=\"{ToSvgColor(shape.StyleFillColor.Value.Resolve(colorScheme))}\"",
                FillType.None => "fill=\"none\"",
                _ => "fill=\"#f0f0f0\""
            }
        };

        var strokeAttr = string.Empty;
        if (shape.Line.Fill is { Type: FillType.Solid, Solid: not null })
        {
            var lw = shape.Line.WidthPoints ?? 1.0;
            strokeAttr = $"stroke=\"{ToSvgColor(shape.Line.Fill.Solid.Color.Resolve(colorScheme))}\" stroke-width=\"{lw:F2}\"";
        }

        var idAttr = options.AnnotateShapes ? $" data-shape-index=\"{index}\"" : string.Empty;
        sb.AppendLine($"<g transform=\"translate({x:F4},{y:F4})\"{idAttr}>");

        // Shape background
        sb.AppendLine($"<rect x=\"0\" y=\"0\" width=\"{w:F4}\" height=\"{h:F4}\" {fillAttr} {strokeAttr}/>");

        // Shape content
        switch (shape)
        {
            case AutoShape { TextFrame.Paragraphs.Count: > 0 } auto:
                // ReSharper disable BadListLineBreaks
                WriteTextFrame(sb, auto, w, h, colorScheme, fontScheme, usedEmbeddedFonts);
                // ReSharper restore BadListLineBreaks
            break;
            case PictureShape { Image: not null } pic when options.EmbedImages:
                WritePicture(sb, pic, w, h);
            break;
        }

        sb.AppendLine("</g>");
    }

    private static void WriteTextFrame(
        StringBuilder sb,
        AutoShape shape,
        double w,
        double h,
        ColorScheme? colorScheme,
        FontScheme fontScheme,
        ISet<string>? usedEmbeddedFonts = null
    )
    {
        const double paddingPt = 4.0;
        var cursorY = paddingPt;

        // Default text color: StyleTextColor → dk1 → black.
        string defaultColor;
        if (shape.StyleTextColor.HasValue)
            defaultColor = ToSvgColor(shape.StyleTextColor.Value.Resolve(colorScheme));
        else if (colorScheme is not null)
            defaultColor = ToSvgColor(colorScheme.Dark1.Resolve(colorScheme));
        else
            defaultColor = "#000000";

        foreach (var para in shape.TextFrame.Paragraphs)
        {
            if (para.Runs.Count == 0)
            {
                cursorY += 14.0 * TextConstants.DefaultLineHeightFactor;
                continue;
            }

            var maxSize = para.Runs.Max(static r => r.Format.FontSizePoints ?? TextConstants.DefaultFontSizePt);
            var lineHeight = maxSize * TextConstants.DefaultLineHeightFactor;
            cursorY += maxSize; // advance to baseline

            if (cursorY > h - paddingPt) break;

            var anchor = para.Alignment switch
            {
                TextAlignment.Center => SvgTextAnchor.Middle,
                TextAlignment.Right => SvgTextAnchor.End,
                _ => SvgTextAnchor.Start
            };

            var textX = para.Alignment switch
            {
                TextAlignment.Center => w / 2,
                TextAlignment.Right => w - paddingPt,
                _ => paddingPt
            };

            sb.Append($"<text x=\"{textX:F4}\" y=\"{cursorY:F4}\" text-anchor=\"{anchor}\">");

            foreach (var run in para.Runs.Where(static run => !string.IsNullOrEmpty(run.Text)))
            {
                var fs = run.Format.FontSizePoints ?? TextConstants.DefaultFontSizePt;
                var weight = run.Format.Bold.Value == true ? "bold" : "normal";
                var style = run.Format.Italic.Value == true ? "italic" : "normal";
                var fill = run.Format.Fill?.Solid != null
                    ? ToSvgColor(run.Format.Fill.Solid.Color.Resolve(colorScheme))
                    : defaultColor;
                var fontFamily = ResolveFontFamily(run.Format.LatinFont, fontScheme);
                usedEmbeddedFonts?.Add(fontFamily);

                sb.Append(
                    $"<tspan font-size=\"{fs:F1}\" font-weight=\"{weight}\" " +
                    $"font-style=\"{style}\" font-family=\"{EscapeCssIdentifier(fontFamily)}\" " +
                    $"fill=\"{fill}\">" +
                    $"{EscapeSvg(run.Text)}</tspan>"
                );
            }

            sb.AppendLine("</text>");
            cursorY += lineHeight - maxSize; // remaining line gap
        }
    }

    private static void WritePicture(
        StringBuilder sb,
        PictureShape pic,
        double w,
        double h
    )
    {
        var dataUri = ExportText.ToBase64DataUri(pic.Image!.Data, pic.Image.ContentType);
        sb.AppendLine(
            $"<image x=\"0\" y=\"0\" width=\"{w:F4}\" height=\"{h:F4}\" " +
            $"preserveAspectRatio=\"none\" " +
            $"href=\"{dataUri}\"/>"
        );
    }

    private static string ToSvgColor(uint argb)
    {
        var (a, r, g, b) = ColorMath.UnpackArgb(argb);
        return a < 255
            ? $"rgba({r},{g},{b},{a / 255.0:F3})"
            : $"#{r:X2}{g:X2}{b:X2}";
    }

    private static string EscapeSvg(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string EscapeCssIdentifier(string value) =>
        value.Replace("\\", @"\\").Replace("\"", "\\\"").Replace("'", "\\'");

    /// <summary>
    ///     Resolves the effective font family name for a run, expanding theme references
    ///     and falling back to the configured default.
    /// </summary>
    private static string ResolveFontFamily(string? latinFont, FontScheme fontScheme) =>
        !string.IsNullOrEmpty(latinFont)
            ? latinFont switch
            {
                "+mj-lt" => fontScheme.MajorFont.LatinFont is { Length: > 0 } mj ? mj : TextConstants.FallbackLatinFont,
                "+mn-lt" => fontScheme.MinorFont.LatinFont is { Length: > 0 } mn ? mn : TextConstants.FallbackLatinFont,
                _ => latinFont
            }
            : TextConstants.FallbackLatinFont;
}
