using System.Globalization;
using System.Xml.Linq;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;
using SaveOptions = System.Xml.Linq.SaveOptions;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IXfdfEditor" /> implementation.
///     Serialises/deserialises annotations using XFDF (XML Forms Data Format, ISO 19444-1).
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class XfdfEditor : IXfdfEditor
{
    private static readonly XNamespace XfdfNs = "http://ns.adobe.com/xfdf/";

    /// <inheritdoc />
    public string ExportAnnotationsToXfdf(IPdfDocument document)
    {
        var annots = new XElement(XfdfNs + "annots");

        for (var page = 1; page <= document.PageCount; page++)
        {
            var pageAnnots = document.Pages[page].GetAnnotations();
            foreach (var ann in pageAnnots)
                annots.Add(ToXfdfElement(ann, page - 1)); // XFDF uses 0-based page index
        }

        var xfdf = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(
                XfdfNs + "xfdf",
                new XAttribute(XNamespace.Xml + "space", "preserve"),
                annots
            )
        );

        return xfdf.ToString(SaveOptions.None);
    }

    /// <inheritdoc />
    public Task ImportAnnotationsFromXfdfAsync(
        IPdfDocument document,
        string xfdfXml,
        CancellationToken ct = default
    ) => Task.Run(() => Import(document, xfdfXml), ct);

    // ── Export helpers ────────────────────────────────────────────────────────

    private static XElement ToXfdfElement(Annotation ann, int zeroBasedPage)
    {
        var elemName = ann.Subtype switch
        {
            AnnotationSubtype.Text => "text",
            AnnotationSubtype.Highlight => "highlight",
            AnnotationSubtype.Link => "link",
            AnnotationSubtype.FreeText => "freetext",
            AnnotationSubtype.Square => "square",
            AnnotationSubtype.Circle => "circle",
            _ => "text"
        };

        var elem = new XElement(
            XfdfNs + elemName,
            new XAttribute("page", zeroBasedPage),
            new XAttribute("rect", FormattableString.Invariant($"{ann.X:G},{ann.Y:G},{ann.X + ann.Width:G},{ann.Y + ann.Height:G}"))
        );

        if (ann.Color is { Length: 3 } c)
            elem.Add(new XAttribute("color", $"#{(int)(c[0] * 255):X2}{(int)(c[1] * 255):X2}{(int)(c[2] * 255):X2}"));

        if (!string.IsNullOrEmpty(ann.Contents))
            elem.Add(new XElement(XfdfNs + "contents", ann.Contents));

        return elem;
    }

    // ── Import helpers ────────────────────────────────────────────────────────

    private static void Import(IPdfDocument document, string xfdfXml)
    {
        var xfdf = XDocument.Parse(xfdfXml);
        var annotsElem = xfdf.Root?.Element(XfdfNs + "annots");
        if (annotsElem is null) return;

        var editor = new AnnotationEditor();
        foreach (var elem in annotsElem.Elements())
        {
            var subtype = elem.Name.LocalName switch
            {
                "text" => AnnotationSubtype.Text,
                "highlight" => AnnotationSubtype.Highlight,
                "link" => AnnotationSubtype.Link,
                "freetext" => AnnotationSubtype.FreeText,
                "square" => AnnotationSubtype.Square,
                "circle" => AnnotationSubtype.Circle,
                _ => AnnotationSubtype.Text
            };

            var pageAttr = elem.Attribute("page")?.Value;
            if (!int.TryParse(pageAttr, out var zeroPage))
                continue;

            var pageNumber = zeroPage + 1; // XFDF is 0-based; Unchained is 1-based

            var rect = ParseRect(elem.Attribute("rect")?.Value);
            if (rect is null)
                continue;

            var contents = elem.Element(XfdfNs + "contents")?.Value;
            var color = ParseHexColor(elem.Attribute("color")?.Value);
            var ann = new Annotation(
                subtype,
                rect.Value.x,
                rect.Value.y,
                rect.Value.w,
                rect.Value.h,
                contents,
                color
            );

            editor.AddAnnotationAsync(document, pageNumber, ann).GetAwaiter().GetResult();
        }
    }

    private static (float x, float y, float w, float h)? ParseRect(string? rectStr)
    {
        if (string.IsNullOrEmpty(rectStr))
            return null;

        var parts = rectStr.Split(',');
        if (parts.Length < 4) return null;
        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var x2) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var y2))
            return null;

        return (x, y, x2 - x, y2 - y);
    }

    private static float[]? ParseHexColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 7 || hex[0] != '#')
            return null;

        if (!int.TryParse(hex[1..3], NumberStyles.HexNumber, null, out var r) ||
            !int.TryParse(hex[3..5], NumberStyles.HexNumber, null, out var g) ||
            !int.TryParse(hex[5..7], NumberStyles.HexNumber, null, out var b))
            return null;

        return [r / 255f, g / 255f, b / 255f];
    }
}
