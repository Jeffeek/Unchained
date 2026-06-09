using System.Text;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Export;

/// <summary>
/// Exports a <see cref="PresentationDocument"/> to HTML5 — one file per slide.
/// Shapes are positioned with CSS absolute positioning; text is embedded as
/// HTML; images are optionally inlined as Base64 data URIs.
/// </summary>
internal static class PptxToHtmlWriter
{
    // EMU → CSS pixels at 96 DPI: 1 px = 914400/96 = 9525 EMU
    private const double EmuToPx = 1.0 / 9525.0;

    /// <summary>
    /// Returns a dictionary mapping slide file names to their HTML content bytes.
    /// The caller writes each entry to a file.
    /// </summary>
    public static Dictionary<string, byte[]> Write(
        PresentationDocument document,
        HtmlSaveOptions options)
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
        Slide slide, double slideW, double slideH, HtmlSaveOptions options)
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
    /// Writes the inner content of a slide (background + shapes) into <paramref name="sb"/>,
    /// without the surrounding document or <c>.slide</c> wrapper. Shared by the per-slide export
    /// and the single-file HTML5 player.
    /// </summary>
    internal static void WriteSlideContent(
        StringBuilder sb, Slide slide, double slideW, double slideH, HtmlSaveOptions options)
    {
        WriteBackground(sb, slide, slideW, slideH);
        foreach (var shape in slide.Shapes)
            WriteShape(sb, shape, options);
    }

    private static void WriteBackground(StringBuilder sb, Slide slide, double w, double h)
    {
        var fill = slide.Background.Fill;
        if (fill.Type != FillType.Solid || fill.Solid == null) return;
        var color = ToCssColor(fill.Solid.Color.Resolve(null));
        sb.AppendLine($"<div style=\"position:absolute;left:0;top:0;width:{w:F2}px;height:{h:F2}px;background:{color}\"></div>");
    }

    private static void WriteShape(StringBuilder sb, Shape shape, HtmlSaveOptions options)
    {
        var x = shape.X.Value * EmuToPx;
        var y = shape.Y.Value * EmuToPx;
        var w = shape.Width.Value * EmuToPx;
        var h = shape.Height.Value * EmuToPx;

        var style = new StringBuilder($"left:{x:F2}px;top:{y:F2}px;width:{w:F2}px;height:{h:F2}px;");

        // Fill
        if (shape.Fill.Type == FillType.Solid && shape.Fill.Solid != null)
            style.Append($"background:{ToCssColor(shape.Fill.Solid.Color.Resolve(null))};");
        else if (shape.Fill.Type == FillType.None)
            style.Append("background:transparent;");

        // Border
        if (shape.Line.Fill.Type == FillType.Solid && shape.Line.Fill.Solid != null)
        {
            var lw = shape.Line.WidthPoints ?? 1.0;
            style.Append($"border:{lw:F1}px solid {ToCssColor(shape.Line.Fill.Solid.Color.Resolve(null))};");
        }

        sb.AppendLine($"<div class=\"shape\" style=\"{style}\">");

        switch (shape)
        {
            case AutoShape auto when auto.TextFrame.Paragraphs.Count > 0:
                WriteTextFrame(sb, auto);
                break;
            case PictureShape pic when pic.Image != null && options.EmbedImages:
                WritePicture(sb, pic);
                break;
        }

        sb.AppendLine("</div>");
    }

    private static void WriteTextFrame(StringBuilder sb, AutoShape shape)
    {
        sb.AppendLine("<div class=\"text-frame\">");
        foreach (var para in shape.TextFrame.Paragraphs)
        {
            var align = para.Alignment switch
            {
                Unchained.Ooxml.Text.TextAlignment.Center => "center",
                Unchained.Ooxml.Text.TextAlignment.Right => "right",
                Unchained.Ooxml.Text.TextAlignment.Justify => "justify",
                _ => "left"
            };
            sb.Append($"<p class=\"para\" style=\"text-align:{align}\">");

            foreach (var run in para.Runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;
                var runStyle = new StringBuilder();
                var fs = run.Format.FontSizePoints ?? 12.0;
                runStyle.Append($"font-size:{fs:F1}pt;");
                if (run.Format.Bold.Value == true) runStyle.Append("font-weight:bold;");
                if (run.Format.Italic.Value == true) runStyle.Append("font-style:italic;");
                if (run.Format.Fill?.Solid != null)
                    runStyle.Append($"color:{ToCssColor(run.Format.Fill.Solid.Color.Resolve(null))};");

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
        sb.AppendLine($"<img style=\"width:100%;height:100%;object-fit:fill\" src=\"data:{contentType};base64,{b64}\" alt=\"{EscapeHtml(pic.AltText ?? string.Empty)}\">");
    }

    private static string ToCssColor(uint argb)
    {
        var r = (argb >> 16) & 0xFF;
        var g = (argb >> 8) & 0xFF;
        var b = argb & 0xFF;
        return $"rgb({r},{g},{b})";
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&#39;");
}
