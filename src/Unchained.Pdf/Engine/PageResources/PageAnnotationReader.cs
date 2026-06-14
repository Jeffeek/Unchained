using System.Text;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine.PageResources;

/// <summary>
///     Reads a page's <c>/Annots</c> array into <see cref="Annotation" /> models
///     (subtype, rectangle, contents, colour). Extracted from <see cref="PdfPageAdapter" />.
/// </summary>
internal static class PageAnnotationReader
{
    internal static IReadOnlyList<Annotation> GetAnnotations(PdfDictionary page, PdfDocumentCore core)
    {
        var annotationsObj = page[PdfName.Annots];
        var annotationsArr = annotationsObj switch
        {
            PdfArray a => a,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfArray,
            _ => null
        };
        if (annotationsArr is null)
            return [];

        var result = new List<Annotation>();
        foreach (var elem in annotationsArr.Elements)
        {
            var dict = core.ResolveDict(elem);
            if (dict is null) continue;

            var subtypeName = dict.GetName(PdfName.Subtype.Value) ?? string.Empty;
            var subtype = subtypeName switch
            {
                "Text" => AnnotationSubtype.Text,
                "Highlight" => AnnotationSubtype.Highlight,
                "Link" => AnnotationSubtype.Link,
                "FreeText" => AnnotationSubtype.FreeText,
                "Square" => AnnotationSubtype.Square,
                "Circle" => AnnotationSubtype.Circle,
                _ => AnnotationSubtype.Text
            };

            var rect = dict.Get<PdfArray>(PdfName.Rect);
            var x = rect is { Count: >= 4 } ? (float)rect[0].ReadIntOrReal() : 0f;
            var y = rect is { Count: >= 4 } ? (float)rect[1].ReadIntOrReal() : 0f;
            var x2 = rect is { Count: >= 4 } ? (float)rect[2].ReadIntOrReal() : 0f;
            var y2 = rect is { Count: >= 4 } ? (float)rect[3].ReadIntOrReal() : 0f;

            string? contents = null;
            if (dict[PdfName.Contents] is PdfString cs)
                contents = Encoding.Latin1.GetString(cs.Bytes.Span);

            float[]? color = null;
            if (dict.Get<PdfArray>(PdfName.C) is { Count: 3 } cArr)
                color = [cArr[0].ReadFloat(), cArr[1].ReadFloat(), cArr[2].ReadFloat()];

            result.Add(
                new Annotation(
                    subtype,
                    x,
                    y,
                    x2 - x,
                    y2 - y,
                    contents,
                    color
                )
            );
        }

        return result;
    }
}
