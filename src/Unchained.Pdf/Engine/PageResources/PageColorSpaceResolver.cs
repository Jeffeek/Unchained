using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine.PageResources;

/// <summary>
///     Collects the named colour spaces declared on a page (and on any nested Form XObjects),
///     building a <see cref="ColorSpaceInfo" /> for each. Handles Device*, ICCBased, Separation,
///     DeviceN, Indexed, CalGray, CalRGB and Lab spaces. Extracted from <see cref="PdfPageAdapter" />.
/// </summary>
internal static class PageColorSpaceResolver
{
    private const int MaxFormXObjectDepth = 10;

    internal static IReadOnlyDictionary<string, ColorSpaceInfo> GetColorSpaces(PdfDictionary page, PdfDocumentCore core)
    {
        var result = new Dictionary<string, ColorSpaceInfo>();
        var resources = core.ResolveDict(page[PdfName.Resources]);
        if (resources is null) return result;

        // Recurse into form XObjects as well (same pattern as CollectShadings).
        CollectColorSpaces(core, resources, result, 0, new HashSet<int>());
        return result;
    }

    private static void CollectColorSpaces(
        PdfDocumentCore core,
        PdfDictionary? resources,
        IDictionary<string, ColorSpaceInfo> result,
        int depth,
        ISet<int> seen
    )
    {
        if (resources is null || depth > MaxFormXObjectDepth)
            return;

        var csDict = core.ResolveDict(resources[PdfName.ColorSpace]);
        if (csDict is not null)
        {
            foreach (var (name, value) in csDict.Entries)
            {
                if (result.ContainsKey(name))
                    continue;

                var csObj = value is PdfIndirectReference r
                    ? core.ResolveIndirect(r.ObjectNumber).Value
                    : value;
                var info = BuildColorSpaceInfo(core, csObj);
                if (info is not null) result[name] = info;
            }
        }

        // Recurse into form XObjects.
        var xObjDict = core.ResolveDict(resources[PdfName.XObject]);
        if (xObjDict is null) return;

        foreach (var (_, xObj) in xObjDict.Entries)
        {
            if (xObj is PdfIndirectReference xr && !seen.Add(xr.ObjectNumber))
                continue;

            var stream = xObj is PdfIndirectReference xrr
                ? core.ResolveIndirect(xrr.ObjectNumber).Value as PdfStream
                : xObj as PdfStream;
            if (stream?.Dictionary.GetName(PdfName.Subtype.Value) != "Form")
                continue;

            CollectColorSpaces(core, core.ResolveDict(stream.Dictionary[PdfName.Resources]), result, depth + 1, seen);
        }
    }

    private static ColorSpaceInfo? BuildColorSpaceInfo(PdfDocumentCore core, PdfObject? csObj)
    {
        if (csObj is PdfIndirectReference r)
            csObj = core.ResolveIndirect(r.ObjectNumber).Value;

        // Direct name — only handle non-Device names as named resources.
        if (csObj is PdfName n)
        {
            return n.Value switch
            {
                "DeviceGray" or "DeviceRGB" or "DeviceCMYK" => null, // handled directly
                "Pattern" => null,
                _ => ColorSpaceInfo.Device(n.Value)
            };
        }

        if (csObj is not PdfArray arr || arr.Count < 1)
            return null;

        var kind = (arr[0] as PdfName)?.Value;
        if (kind is null) return null;

        switch (kind)
        {
            case "DeviceGray": return ColorSpaceInfo.Device("DeviceGray");
            case "DeviceRGB": return ColorSpaceInfo.Device("DeviceRGB");
            case "DeviceCMYK": return ColorSpaceInfo.Device("DeviceCMYK");

            case "ICCBased" when arr.Count >= 2:
            {
                var iccStream = arr[1] is PdfIndirectReference iccRef
                    ? core.ResolveIndirect(iccRef.ObjectNumber).Value as PdfStream
                    : arr[1] as PdfStream;
                // Use /Alternate if present; otherwise infer from /N channel count.
                var altObj = iccStream?.Dictionary[PdfName.Alternate];
                var altName = (altObj as PdfName)?.Value;

                // ReSharper disable once InvertIf
                if (altName is null)
                {
                    var n2 = (int)(iccStream?.Dictionary.Get<PdfInteger>(PdfName.N)?.Value ?? 0);
                    altName = n2 switch { 1 => "DeviceGray", 4 => "DeviceCMYK", _ => "DeviceRGB" };
                }

                return ColorSpaceInfo.IccBased(altName);
            }

            case "Separation" when arr.Count >= 4:
            {
                var altName = core.ResolveBaseSpaceName(arr[2]) ?? "DeviceRGB";
                var fn = PdfFunction.Build(arr[3], core);
                return ColorSpaceInfo.Separation(fn, altName);
            }

            case "DeviceN" when arr.Count >= 4:
            {
                // arr[1] = names array, arr[2] = alternate space, arr[3] = tint transform
                var altName = core.ResolveBaseSpaceName(arr[2]) ?? "DeviceRGB";
                var fn = PdfFunction.Build(arr[3], core);
                return ColorSpaceInfo.DeviceN(fn, altName);
            }

            case "Indexed" when arr.Count >= 4:
            {
                var baseName = core.ResolveBaseSpaceName(arr[1]) ?? "DeviceRGB";
                var baseChannels = baseName switch
                {
                    "DeviceGray" => 1, "DeviceCMYK" => 4, _ => 3
                };
                var lookupObj = arr[3];
                if (lookupObj is PdfIndirectReference lr)
                    lookupObj = core.ResolveIndirect(lr.ObjectNumber).Value;
                var lookup = lookupObj switch
                {
                    PdfString s => s.GetBinaryBytes().ToArray(),
                    PdfStream st => StreamFilters.Decode(st).ToArray(),
                    _ => []
                };
                return lookup.Length > 0
                    ? ColorSpaceInfo.Indexed(lookup, baseChannels, baseName)
                    : null;
            }

            case "CalGray" when arr.Count >= 2:
            {
                var dict = core.ResolveDict(arr[1]);
                var gamma = (dict?[PdfName.Gamma]).ReadFloat();
                return ColorSpaceInfo.CalGrayInfo(gamma > 0 ? gamma : 1.0);
            }

            case "CalRGB" when arr.Count >= 2:
            {
                var dict = core.ResolveDict(arr[1]);
                var gammaArr = dict?[PdfName.Gamma] is PdfArray ga
                    ? ga.Elements.Select(static e => (double)e.ReadFloat()).ToArray()
                    : null;
                var matArr = dict?[PdfName.Matrix] is PdfArray ma
                    ? ma.Elements.Select(static e => (double)e.ReadFloat()).ToArray()
                    : null;
                return ColorSpaceInfo.CalRgb(gammaArr, matArr);
            }

            case "Lab": return ColorSpaceInfo.Lab();

            default: return null;
        }
    }
}
