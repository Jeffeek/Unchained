using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Content;

/// <summary>
///     Evaluates PDF function objects (ISO 32000-1 §7.10) over a single input value, returning
///     the output colour components. Supports type 2 (exponential interpolation) and type 3
///     (stitching), which together cover the overwhelming majority of shading functions.
///     Types 0 (sampled) and 4 (PostScript calculator) are not yet supported and fall back to a
///     linear C0→C1 interpolation when possible, or a mid-grey.
/// </summary>
internal sealed class PdfFunction
{
    private readonly double[] _bounds;

    // Type 2 (exponential):
    private readonly double[] _c0;
    private readonly double[] _c1;
    private readonly double[] _domain;
    private readonly double[] _encode;

    // Type 3 (stitching):
    private readonly PdfFunction[] _functions;
    private readonly int _functionType;
    private readonly double _n;

    private PdfFunction(
        int functionType,
        double[] domain,
        double[] c0,
        double[] c1,
        double n,
        PdfFunction[] functions,
        double[] bounds,
        double[] encode
    )
    {
        _functionType = functionType;
        _domain = domain;
        _c0 = c0;
        _c1 = c1;
        _n = n;
        _functions = functions;
        _bounds = bounds;
        _encode = encode;
    }

    /// <summary>
    ///     Builds a <see cref="PdfFunction" /> from a function dictionary/stream, resolving
    ///     indirect references via <paramref name="core" />. Returns null when the object is not a
    ///     usable function.
    /// </summary>
    internal static PdfFunction? Build(PdfObject? obj, PdfDocumentCore core, int depth = 0)
    {
        if (depth > 16)
            return null;

        if (obj is PdfIndirectReference r)
            obj = core.ResolveIndirect(r.ObjectNumber).Value;

        var dict = obj switch
        {
            PdfStream s => s.Dictionary,
            PdfDictionary d => d,
            _ => null
        };
        if (dict is null) return null;

        var ft = (int)(dict.Get<PdfInteger>(PdfName.Get("FunctionType"))?.Value ?? -1);
        var domain = ReadDoubles(dict["Domain"]) ?? [0, 1];

        switch (ft)
        {
            case 2:
            {
                var c0 = ReadDoubles(dict["C0"]) ?? [0.0];
                var c1 = ReadDoubles(dict["C1"]) ?? [1.0];
                var n = ReadDouble(dict["N"]) ?? 1.0;
                return new PdfFunction(2,
                    domain,
                    c0,
                    c1,
                    n,
                    [],
                    [],
                    []);
            }
            case 3:
            {
                var fnsArr = (Resolve(dict["Functions"], core) as PdfArray)?.Elements ?? [];
                var fns = fnsArr.Select(f => Build(f, core, depth + 1)).Where(static f => f is not null).Select(static f => f!).ToArray();
                var bounds = ReadDoubles(dict["Bounds"]) ?? [];
                var encode = ReadDoubles(dict["Encode"]) ?? [];
                return fns.Length == 0
                    ? null
                    : new PdfFunction(3,
                        domain,
                        [],
                        [],
                        1,
                        fns,
                        bounds,
                        encode);
            }
            default:
                // Types 0 / 4: approximate with the dict's C0/C1 if present, else null.
            {
                var c0 = ReadDoubles(dict["C0"]);
                var c1 = ReadDoubles(dict["C1"]);
                if (c0 is not null && c1 is not null)
                {
                    return new PdfFunction(2,
                        domain,
                        c0,
                        c1,
                        1,
                        [],
                        [],
                        []);
                }

                return null;
            }
        }
    }

    /// <summary>Evaluates the function at <paramref name="t" />, clamped to the domain.</summary>
    internal double[] Eval(double t)
    {
        var x = Math.Clamp(t, _domain[0], _domain[1]);
        switch (_functionType)
        {
            case 2:
            {
                // C0 + x'^N × (C1 − C0), where x' is x normalised over the domain.
                var span = _domain[1] - _domain[0];
                var xn = span > 1e-9 ? (x - _domain[0]) / span : 0;
                var f = Math.Abs(_n - 1.0) < 1e-9 ? xn : Math.Pow(xn, _n);
                var len = Math.Max(_c0.Length, _c1.Length);
                var result = new double[len];
                for (var i = 0; i < len; i++)
                {
                    var a = i < _c0.Length ? _c0[i] : 0;
                    var b = i < _c1.Length ? _c1[i] : 0;
                    result[i] = a + (f * (b - a));
                }

                return result;
            }
            case 3:
            {
                // Find the sub-function whose half-open bound range contains x.
                var k = 0;
                while (k < _bounds.Length && x >= _bounds[k]) k++;
                var lo = k == 0 ? _domain[0] : _bounds[k - 1];
                var hi = k < _bounds.Length ? _bounds[k] : _domain[1];
                var e0 = 2 * k < _encode.Length ? _encode[2 * k] : 0;
                var e1 = (2 * k) + 1 < _encode.Length ? _encode[(2 * k) + 1] : 1;
                var sub = _functions[Math.Min(k, _functions.Length - 1)];
                var span = hi - lo;
                var enc = span > 1e-9 ? e0 + ((x - lo) / span * (e1 - e0)) : e0;
                return sub.Eval(enc);
            }
            default:
                return [0.5];
        }
    }

    private static PdfObject? Resolve(PdfObject? obj, PdfDocumentCore core) =>
        obj is PdfIndirectReference r ? core.ResolveIndirect(r.ObjectNumber).Value : obj;

    private static double[]? ReadDoubles(PdfObject? obj) => obj switch
    {
        PdfArray a => a.Elements.Select(static e => e.ToDouble()).ToArray(),
        _ => null
    };

    private static double? ReadDouble(PdfObject? obj) => obj.ToDoubleOrNull();
}
