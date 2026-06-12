using System.Text;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Export;

/// <summary>
///     Exports a <see cref="PresentationDocument" /> to HTML5 — one file per slide.
///     Shapes are positioned with CSS absolute positioning; text is embedded as
///     HTML; images are optionally inlined as Base64 data URIs.
/// </summary>
internal static class PptxToHtmlWriter
{
    // EMU → CSS pixels at 96 DPI: 1 px = 914400/96 = 9525 EMU
    private const double EmuToPx = EmuConversions.EmuToCssPx;

    /// <summary>
    ///     Returns a dictionary mapping slide file names to their HTML content bytes.
    ///     The caller writes each entry to a file.
    /// </summary>
    public static Dictionary<string, byte[]> Write(
        PresentationDocument document,
        HtmlSaveOptions options
    )
    {
        var result = new Dictionary<string, byte[]>();
        var slides = document.Slides;
        var slideW = document.SlideSize.Width.Value * EmuToPx;
        var slideH = document.SlideSize.Height.Value * EmuToPx;

        var included = Enumerable.Range(0, slides.Count)
            .Where(i => !slides[i].IsHidden || options.IncludeHiddenSlides)
            .ToList();

        for (var idx = 0; idx < included.Count; idx++)
        {
            options.Progress?.Report((double)idx / included.Count);
            var slide = slides[included[idx]];
            var fileName = $"slide{included[idx] + 1}.html";
            var html = BuildSlideHtml(slide, slideW, slideH, options);
            result[fileName] = Encoding.UTF8.GetBytes(html);
        }

        options.Progress?.Report(1.0);
        return result;
    }

    private static string BuildSlideHtml(
        Slide slide,
        double slideW,
        double slideH,
        HtmlSaveOptions options
    )
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine($"<meta name=\"viewport\" content=\"width={slideW:F0}, initial-scale=1\">");
        sb.AppendLine($"<title>{EscapeHtml(slide.Name.Length > 0 ? slide.Name : "Slide")}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("*{box-sizing:border-box;margin:0;padding:0}");
        sb.AppendLine($".slide{{position:relative;width:{slideW:F2}px;height:{slideH:F2}px;overflow:hidden;background:white}}");
        sb.AppendLine(".shape{position:absolute;overflow:hidden}");
        sb.AppendLine(".text-frame{width:100%;height:100%;padding:4px}");
        sb.AppendLine(".para{margin:0;line-height:1.25}");
        if (options.AdditionalCss != null) sb.AppendLine(options.AdditionalCss);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"slide\">");

        WriteSlideContent(sb, slide, slideW, slideH, options);

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    /// <summary>
    ///     Writes the inner content of a slide (background + shapes) into <paramref name="sb" />,
    ///     without the surrounding document or <c>.slide</c> wrapper. Shared by the per-slide export
    ///     and the single-file HTML5 player.
    /// </summary>
    internal static void WriteSlideContent(
        StringBuilder sb,
        Slide slide,
        double slideW,
        double slideH,
        HtmlSaveOptions options
    )
    {
        var colorScheme = slide.Master.Theme.Colors;
        WriteBackground(sb, slide, slideW, slideH, colorScheme);
        foreach (var shape in slide.Shapes)
            WriteShape(sb, shape, options, colorScheme);
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

        var color = ToCssColor(fill.Solid.Color.Resolve(colorScheme));
        sb.AppendLine($"<div style=\"position:absolute;left:0;top:0;width:{w:F2}px;height:{h:F2}px;background:{color}\"></div>");
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
        HtmlSaveOptions options,
        ColorScheme? colorScheme
    )
    {
        var x = shape.X.Value * EmuToPx;
        var y = shape.Y.Value * EmuToPx;
        var w = shape.Width.Value * EmuToPx;
        var h = shape.Height.Value * EmuToPx;

        var style = new StringBuilder($"left:{x:F2}px;top:{y:F2}px;width:{w:F2}px;height:{h:F2}px;");

        if (shape.Fill is { Type: FillType.Solid, Solid: not null })
            style.Append($"background:{ToCssColor(shape.Fill.Solid.Color.Resolve(colorScheme))};");
        else
        {
            switch (shape.Fill.Type)
            {
                case FillType.None when shape.StyleFillColor.HasValue:
                    style.Append($"background:{ToCssColor(shape.StyleFillColor.Value.Resolve(colorScheme))};");
                break;
                case FillType.None:
                    style.Append("background:transparent;");
                break;
                case FillType.Solid:
                case FillType.Gradient:
                case FillType.Pattern:
                case FillType.Picture:
                case FillType.Group:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // Border
        if (shape.Line.Fill is { Type: FillType.Solid, Solid: not null })
        {
            var lw = shape.Line.WidthPoints ?? 1.0;
            style.Append($"border:{lw:F1}px solid {ToCssColor(shape.Line.Fill.Solid.Color.Resolve(colorScheme))};");
        }

        sb.AppendLine($"<div class=\"shape\" style=\"{style}\">");

        switch (shape)
        {
            case AutoShape { TextFrame.Paragraphs.Count: > 0 } auto:
                WriteTextFrame(sb, auto, colorScheme);
            break;
            case PictureShape { Image: not null } pic when options.EmbedImages:
                WritePicture(sb, pic);
            break;
        }

        sb.AppendLine("</div>");
    }

    private static void WriteTextFrame(StringBuilder sb, AutoShape shape, ColorScheme? colorScheme)
    {
        // Default text color: StyleTextColor → dk1 → black.
        string defaultColor;
        if (shape.StyleTextColor.HasValue)
            defaultColor = ToCssColor(shape.StyleTextColor.Value.Resolve(colorScheme));
        else if (colorScheme is not null)
            defaultColor = ToCssColor(colorScheme.Dark1.Resolve(colorScheme));
        else
            defaultColor = "#000000";

        sb.AppendLine("<div class=\"text-frame\">");
        foreach (var para in shape.TextFrame.Paragraphs)
        {
            var align = para.Alignment switch
            {
                TextAlignment.Center => "center",
                TextAlignment.Right => "right",
                TextAlignment.Justify => "justify",
                _ => "left"
            };
            sb.Append($"<p class=\"para\" style=\"text-align:{align}\">");

            foreach (var run in para.Runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;

                var runStyle = new StringBuilder();
                var fs = run.Format.FontSizePoints ?? TextConstants.DefaultFontSizePt;
                runStyle.Append($"font-size:{fs:F1}pt;");
                if (run.Format.Bold.Value == true) runStyle.Append("font-weight:bold;");
                if (run.Format.Italic.Value == true) runStyle.Append("font-style:italic;");
                var textColor = run.Format.Fill?.Solid != null
                    ? ToCssColor(run.Format.Fill.Solid.Color.Resolve(colorScheme))
                    : defaultColor;
                runStyle.Append($"color:{textColor};");

                sb.Append($"<span style=\"{runStyle}\">{EscapeHtml(run.Text)}</span>");
            }

            sb.AppendLine("</p>");
        }

        sb.AppendLine("</div>");
    }

    private static void WritePicture(StringBuilder sb, PictureShape pic)
    {
        var data = pic.Image!.Data;
        var contentType = pic.Image.ContentType;
        var b64 = Convert.ToBase64String(data.ToArray());
        sb.AppendLine(
            $"<img style=\"width:100%;height:100%;object-fit:fill\" src=\"data:{contentType};base64,{b64}\" alt=\"{EscapeHtml(pic.AltText ?? string.Empty)}\">");
    }

    private static string ToCssColor(uint argb)
    {
        var a = (argb >> 24) & 0xFF;
        var r = (argb >> 16) & 0xFF;
        var g = (argb >> 8) & 0xFF;
        var b = argb & 0xFF;
        return a < 255
            ? $"rgba({r},{g},{b},{a / 255.0:F3})"
            : $"rgb({r},{g},{b})";
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&#39;");
}
