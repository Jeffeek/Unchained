using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine.PageResources;

/// <summary>
///     Reads the transparency-related graphics state declared in a page's <c>/ExtGState</c>
///     resources: constant fill/stroke alpha (<c>ca</c>/<c>CA</c>), blend mode (<c>BM</c>) and
///     soft masks (<c>/SMask</c>). Extracted from <see cref="PdfPageAdapter" />.
/// </summary>
internal static class PageTransparencyResolver
{
    internal static IReadOnlyDictionary<string, (double Fill, double Stroke, string BlendMode, string? SoftMaskName)> GetExtGStateAlphas(
        PdfDictionary page,
        PdfDocumentCore core
    )
    {
        var result = new Dictionary<string, (double, double, string, string?)>();
        var resources = core.ResolveDict(page[PdfName.Resources]);
        var extDict = core.ResolveDict(resources?[PdfName.ExtGState]);
        if (extDict is null) return result;

        foreach (var (name, value) in extDict.Entries)
        {
            var gs = core.ResolveDict(value);
            if (gs is null)
                continue;

            var ca = ReadAlpha(gs["ca"]);
            var cA = ReadAlpha(gs["CA"]);
            var bm = (gs[PdfName.BM] as PdfName)?.Value ?? "Normal";
            // /SMask: if it's a dict (not /None name), note its presence using the ExtGState name.
            string? smaskName = null;
            var smaskObj = gs[PdfName.SMask];
            if (smaskObj is PdfDictionary)
                smaskName = name;
            if (ca is null && cA is null && bm == "Normal" && smaskName is null)
                continue;

            result[name] = (ca ?? 1.0, cA ?? 1.0, bm, smaskName);
        }

        return result;
    }

    internal static IReadOnlyDictionary<string, SoftMaskInfo> GetSoftMasks(
        PdfDictionary page,
        PdfDocumentCore core,
        int widthPx,
        int heightPx
    )
    {
        var result = new Dictionary<string, SoftMaskInfo>();
        var resources = core.ResolveDict(page[PdfName.Resources]);
        var extDict = core.ResolveDict(resources?[PdfName.ExtGState]);
        if (extDict is null) return result;

        foreach (var (name, value) in extDict.Entries)
        {
            var gs = core.ResolveDict(value);
            var smaskObj = gs?[PdfName.SMask];
            if (smaskObj is not PdfDictionary smaskDict) continue;

            var maskType = (smaskDict[PdfName.S] as PdfName)?.Value ?? "Alpha";
            var formRef = smaskDict[PdfName.G];
            var formStream = formRef is PdfIndirectReference fRef
                ? core.ResolveIndirect(fRef.ObjectNumber).Value as PdfStream
                : formRef as PdfStream;
            if (formStream?.Dictionary.GetName(PdfName.Subtype.Value) != "Form") continue;

            var bbox = formStream.GetFormBBox();
            var matrix = formStream.GetFormMatrix();

            try
            {
                var decodedBytes = StreamFilters.Decode(formStream);
                var operators = ContentStreamParser.Parse(decodedBytes);

                // Use a page adapter with page number 0 (not a real page — just resource access).
                var formAdapter = new PdfPageAdapter(formStream.Dictionary, 0, core);

                result[name] = new SoftMaskInfo(
                    widthPx,
                    heightPx,
                    maskType,
                    operators,
                    formAdapter,
                    bbox,
                    matrix);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }
        }

        return result;
    }

    private static double? ReadAlpha(PdfObject? o) => o switch
    {
        PdfReal r => Math.Clamp(r.Value, 0, 1),
        PdfInteger i => Math.Clamp((double)i.Value, 0, 1),
        _ => null
    };
}
