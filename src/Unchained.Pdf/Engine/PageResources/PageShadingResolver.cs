using Unchained.Drawing.Primitives;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine.PageResources;

/// <summary>
///     Collects axial/radial/mesh shadings and tiling patterns declared on a page and on any
///     nested Form XObjects. Axial/radial shadings are pre-sampled into a 256-entry RGB ramp;
///     mesh shadings are decoded into Gouraud triangles. Extracted from <see cref="PdfPageAdapter" />.
/// </summary>
internal static class PageShadingResolver
{
    private const int MaxFormXObjectDepth = 10;

    internal static IReadOnlyDictionary<string, ShadingInfo> GetShadings(PdfDictionary page, PdfDocumentCore core)
    {
        var result = new Dictionary<string, ShadingInfo>();
        var resources = core.ResolveDict(page[PdfName.Resources]);
        // Collect from the page resources and from every form XObject's resources, since
        // GetContentOperators inlines form content (so their sh/scn operators reach the
        // renderer) and those operators reference shading/pattern names declared on the form.
        CollectShadings(core, resources, result, 0, (HashSet<int>)[]);
        return result;
    }

    internal static IReadOnlyDictionary<string, TilingPatternInfo> GetTilingPatterns(PdfDictionary page, PdfDocumentCore core)
    {
        var result = new Dictionary<string, TilingPatternInfo>();
        CollectTilingPatterns(core, core.ResolveDict(page[PdfName.Resources]), result, 0, (HashSet<int>)[]);
        return result;
    }

    private static void CollectShadings(
        PdfDocumentCore core,
        PdfDictionary? resources,
        IDictionary<string, ShadingInfo> result,
        int depth,
        ISet<int> seen
    )
    {
        if (resources is null || depth > MaxFormXObjectDepth) return;

        // /Shading resources — painted directly by the `sh` operator.
        var shadingDict = core.ResolveDict(resources[PdfName.Shading]);
        if (shadingDict is not null)
        {
            foreach (var (name, value) in shadingDict.Entries)
            {
                if (!result.ContainsKey(name) && BuildShading(core, core.ResolveAny(value)) is { } s)
                    result[name] = s;
            }
        }

        // /Pattern resources with /PatternType 2 — shading used as a fill colour.
        var patternDict = core.ResolveDict(resources[PdfName.Pattern]);
        if (patternDict is not null)
        {
            foreach (var (name, value) in patternDict.Entries)
            {
                if (result.ContainsKey(name)) continue;

                var pat = core.ResolveDictOrStreamDict(value);
                if (pat is null) continue;
                if ((int)(pat.Get<PdfInteger>(PdfName.PatternType)?.Value ?? 0) != 2) continue;

                if (BuildShading(core, core.ResolveAny(pat["Shading"])) is { } s)
                    result[name] = s;
            }
        }

        // Recurse into form XObjects' own resource dictionaries.
        var xObjDict = core.ResolveDict(resources[PdfName.XObject]);
        if (xObjDict is null)
            return;

        foreach (var (_, value) in xObjDict.Entries)
        {
            if (value is PdfIndirectReference r && !seen.Add(r.ObjectNumber))
                continue;

            var stream = value is PdfIndirectReference rr
                ? core.ResolveIndirect(rr.ObjectNumber).Value as PdfStream
                : value as PdfStream;
            if (stream?.Dictionary.GetName(PdfName.Subtype.Value) != "Form")
                continue;

            CollectShadings(core, core.ResolveDict(stream.Dictionary[PdfName.Resources]), result, depth + 1, seen);
        }
    }

    private static void CollectTilingPatterns(
        PdfDocumentCore core,
        PdfDictionary? resources,
        IDictionary<string, TilingPatternInfo> result,
        int depth,
        ISet<int> seen
    )
    {
        if (resources is null || depth > MaxFormXObjectDepth) return;

        var patternDict = core.ResolveDict(resources[PdfName.Pattern]);
        if (patternDict is not null)
        {
            foreach (var (name, value) in patternDict.Entries)
            {
                if (result.ContainsKey(name))
                    continue;

                var resolved = core.ResolveAny(value);
                if (resolved is not PdfStream stream) continue; // tiling patterns are streams

                var d = stream.Dictionary;
                if ((int)(d.Get<PdfInteger>(PdfName.PatternType)?.Value ?? 0) != 1) continue;

                var bbox = d["BBox"].ReadFloatArray();
                if (bbox is null || bbox.Length < 4) continue;

                var paintType = (int)(d.Get<PdfInteger>(PdfName.PaintType)?.Value ?? 1);
                var xstep = d["XStep"].ReadFloat();
                var ystep = d["YStep"].ReadFloat();
                var matrix = d["Matrix"].ReadFloatArray() ?? [1, 0, 0, 1, 0, 0];

                IReadOnlyList<ContentOperator> ops;
                try { ops = ContentStreamParser.Parse(StreamFilters.Decode(stream)); }
                catch { continue; }

                result[name] = new TilingPatternInfo(
                    paintType,
                    bbox.Select(static f => (double)f).ToArray(),
                    xstep,
                    ystep,
                    matrix.Select(static f => (double)f).ToArray(),
                    ops
                );
            }
        }

        // Recurse into form XObjects (same flattening rationale as shadings).
        var xObjDict = core.ResolveDict(resources[PdfName.XObject]);
        if (xObjDict is null)
            return;

        foreach (var (_, value) in xObjDict.Entries)
        {
            if (value is PdfIndirectReference r && !seen.Add(r.ObjectNumber))
                continue;

            var stream = value is PdfIndirectReference rr
                ? core.ResolveIndirect(rr.ObjectNumber).Value as PdfStream
                : value as PdfStream;
            if (stream?.Dictionary.GetName(PdfName.Subtype.Value) != "Form")
                continue;

            CollectTilingPatterns(core, core.ResolveDict(stream.Dictionary[PdfName.Resources]), result, depth + 1, seen);
        }
    }

    // Builds a ShadingInfo (axial/radial only) from a shading dictionary, pre-sampling its
    // colour function into a 256-entry RGB ramp. Returns null for unsupported shading types.
    private static ShadingInfo? BuildShading(PdfDocumentCore core, PdfObject? obj)
    {
        var dict = obj switch
        {
            PdfStream s => s.Dictionary,
            PdfDictionary d => d,
            _ => null
        };
        if (dict is null) return null;

        var type = (int)(dict.Get<PdfInteger>(PdfName.ShadingType)?.Value ?? 0);

        // Mesh shadings (4/5/6/7) carry their geometry+colour in a data stream; decode it
        // into Gouraud triangles for the renderer.
        if (type is 4 or 5 or 6 or 7)
        {
            if (obj is not PdfStream meshStream)
                return null;

            var tris = MeshShadingDecoder.Decode(meshStream, core, type);
            return tris.Count == 0
                ? null
                : new ShadingInfo(
                    type,
                    [],
                    false,
                    false,
                    new byte[256 * 3],
                    tris
                );
        }

        if (type is not (2 or 3)) return null; // only axial/radial below

        var coords = dict["Coords"].ReadFloatArray();
        if (coords is null || coords.Length < (type == 2 ? 4 : 6)) return null;

        var domain = dict["Domain"].ReadFloatArray() ?? [0, 1];
        var (extStart, extEnd) = ReadExtend(dict["Extend"]);
        var cs = core.ReadColorSpace(dict) ?? "DeviceRGB";

        var fn = PdfFunction.Build(dict["Function"], core);

        // Pre-sample 256 colours from domain start → end.
        var ramp = new byte[256 * 3];
        for (var i = 0; i < 256; i++)
        {
            var t = domain[0] + (i / 255.0 * (domain[1] - domain[0]));
            var comps = fn?.Eval(t) ?? [0.5, 0.5, 0.5];
            var (r, g, b) = ComponentsToRgb(comps, cs);
            ramp[i * 3] = r;
            ramp[(i * 3) + 1] = g;
            ramp[(i * 3) + 2] = b;
        }

        return new ShadingInfo(type, coords.Select(static f => (double)f).ToArray(), extStart, extEnd, ramp);
    }

    private static (byte R, byte G, byte B) ComponentsToRgb(IReadOnlyList<double> c, string cs)
    {
        return (cs, c.Count) switch
        {
            ("DeviceCMYK", >= 4) => CmykToBytes(c[0], c[1], c[2], c[3]),
            (_, >= 3) => (B255(c[0]), B255(c[1]), B255(c[2])),
            (_, 1) => (B255(c[0]), B255(c[0]), B255(c[0])),
            _ => (128, 128, 128)
        };

        static byte B255(double v) => ColorMath.ToByteRounded(v);

        static (byte R, byte G, byte B) CmykToBytes(
            double c,
            double m,
            double y,
            double k
        )
        {
            var (r, g, b) = ColorMath.CmykToRgb(c, m, y, k);
            return (B255(r), B255(g), B255(b));
        }
    }

    private static (bool Start, bool End) ReadExtend(PdfObject? obj) =>
        obj is PdfArray { Count: >= 2 } a
            ? ((a[0] as PdfBoolean)?.Value ?? false, (a[1] as PdfBoolean)?.Value ?? false)
            : (false, false);
}
