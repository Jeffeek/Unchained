using System.Text;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Export;

/// <summary>
/// Exports presentation slides to SVG — one SVG document per slide.
/// Shapes are rendered as SVG elements; text is embedded as
/// <c>&lt;text&gt;</c>; images are optionally inlined as Base64 data URIs.
/// </summary>
internal static class PptxToSvgWriter
{
    // EMU → SVG user units (points): 1 pt = 12700 EMU
    private const double EmuToPt = 1.0 / 12700.0;

    /// <summary>
    /// Returns the SVG bytes for a single slide.
    /// </summary>
    public static byte[] WriteSlide(Slide slide, SlideSize slideSize, SvgSaveOptions options)
    {
        var w = slideSize.Width.Value * EmuToPt;
        var h = slideSize.Height.Value * EmuToPt;

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");

        if (options.Responsive)
        {
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                          $"viewBox=\"0 0 {w:F4} {h:F4}\">");
        }
        else
        {
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                          $"width=\"{w:F4}pt\" height=\"{h:F4}pt\" viewBox=\"0 0 {w:F4} {h:F4}\">");
        }

        // Background
        sb.AppendLine($"<rect x=\"0\" y=\"0\" width=\"{w:F4}\" height=\"{h:F4}\" fill=\"white\"/>");
        WriteBackground(sb, slide, w, h);

        // Shapes
        foreach (var shape in slide.Shapes)
            WriteShape(sb, shape, options);

        sb.AppendLine("</svg>");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Returns SVG bytes for every non-hidden slide in the document.
    /// </summary>
    public static byte[][] WriteAll(PresentationDocument document, SvgSaveOptions options)
    {
        var slides = document.Slides;
        var result = new List<byte[]>();
        for (var i = 0; i < slides.Count; i++)
        {
            if (slides[i].IsHidden && !options.IncludeHiddenSlides) continue;
            result.Add(WriteSlide(slides[i], document.SlideSize, options));
        }
        return [.. result];
    }

    private static void WriteBackground(StringBuilder sb, Slide slide, double w, double h)
    {
        var fill = slide.Background.Fill;
        if (fill.Type != FillType.Solid || fill.Solid == null) return;
        var color = ToSvgColor(fill.Solid.Color.Resolve(null));
        sb.AppendLine($"<rect x=\"0\" y=\"0\" width=\"{w:F4}\" height=\"{h:F4}\" fill=\"{color}\"/>");
    }

    private static void WriteShape(StringBuilder sb, Shape shape, SvgSaveOptions options)
    {
        var x = shape.X.Value * EmuToPt;
        var y = shape.Y.Value * EmuToPt;
        var w = shape.Width.Value * EmuToPt;
        var h = shape.Height.Value * EmuToPt;

        var fillAttr = shape.Fill.Type switch
        {
            FillType.Solid when shape.Fill.Solid != null =>
                $"fill=\"{ToSvgColor(shape.Fill.Solid.Color.Resolve(null))}\"",
            FillType.None => "fill=\"none\"",
            _ => "fill=\"#f0f0f0\""
        };

        var strokeAttr = string.Empty;
        if (shape.Line.Fill.Type == FillType.Solid && shape.Line.Fill.Solid != null)
        {
            var lw = shape.Line.WidthPoints ?? 1.0;
            strokeAttr = $"stroke=\"{ToSvgColor(shape.Line.Fill.Solid.Color.Resolve(null))}\" stroke-width=\"{lw:F2}\"";
        }

        sb.AppendLine($"<g transform=\"translate({x:F4},{y:F4})\">");

        // Shape background
        sb.AppendLine($"<rect x=\"0\" y=\"0\" width=\"{w:F4}\" height=\"{h:F4}\" {fillAttr} {strokeAttr}/>");

        // Shape content
        switch (shape)
        {
            case AutoShape auto when auto.TextFrame.Paragraphs.Count > 0:
                WriteTextFrame(sb, auto, w, h);
                break;
            case PictureShape pic when pic.Image != null && options.EmbedImages:
                WritePicture(sb, pic, w, h);
                break;
        }

        sb.AppendLine("</g>");
    }

    private static void WriteTextFrame(StringBuilder sb, AutoShape shape, double w, double h)
    {
        const double PaddingPt = 4.0;
        var cursorY = PaddingPt;

        foreach (var para in shape.TextFrame.Paragraphs)
        {
            if (para.Runs.Count == 0)
            {
                cursorY += 14.0 * 1.25;
                continue;
            }

            var maxSize = para.Runs.Max(r => r.Format.FontSizePoints ?? 12.0);
            var lineHeight = maxSize * 1.25;
            cursorY += maxSize; // advance to baseline

            if (cursorY > h - PaddingPt) break;

            var anchor = para.Alignment switch
            {
                Unchained.Ooxml.Text.TextAlignment.Center => "middle",
                Unchained.Ooxml.Text.TextAlignment.Right => "end",
                _ => "start"
            };

            var textX = para.Alignment switch
            {
                Unchained.Ooxml.Text.TextAlignment.Center => w / 2,
                Unchained.Ooxml.Text.TextAlignment.Right => w - PaddingPt,
                _ => PaddingPt
            };

            sb.Append($"<text x=\"{textX:F4}\" y=\"{cursorY:F4}\" text-anchor=\"{anchor}\">");

            foreach (var run in para.Runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;
                var fs = run.Format.FontSizePoints ?? 12.0;
                var weight = run.Format.Bold.Value == true ? "bold" : "normal";
                var style = run.Format.Italic.Value == true ? "italic" : "normal";
                var fill = run.Format.Fill?.Solid != null
                    ? ToSvgColor(run.Format.Fill.Solid.Color.Resolve(null))
                    : "#000000";

                sb.Append($"<tspan font-size=\"{fs:F1}\" font-weight=\"{weight}\" " +
                           $"font-style=\"{style}\" fill=\"{fill}\">" +
                           $"{EscapeSvg(run.Text)}</tspan>");
            }

            sb.AppendLine("</text>");
            cursorY += lineHeight - maxSize; // remaining line gap
        }
    }

    private static void WritePicture(StringBuilder sb, PictureShape pic, double w, double h)
    {
        var data = pic.Image!.Data;
        var contentType = pic.Image.ContentType;
        var b64 = Convert.ToBase64String(data.ToArray());
        sb.AppendLine($"<image x=\"0\" y=\"0\" width=\"{w:F4}\" height=\"{h:F4}\" " +
                      $"preserveAspectRatio=\"none\" " +
                      $"href=\"data:{contentType};base64,{b64}\"/>");
    }

    private static string ToSvgColor(uint argb)
    {
        var r = (argb >> 16) & 0xFF;
        var g = (argb >> 8) & 0xFF;
        var b = argb & 0xFF;
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static string EscapeSvg(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
