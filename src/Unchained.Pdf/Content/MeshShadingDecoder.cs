using Unchained.Drawing.Primitives;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Content;

/// <summary>
///     Decodes mesh shadings (ISO 32000-1 §8.7.4.5.5–.8) into a flat list of Gouraud-shaded
///     triangles. Supports type 4 (free-form Gouraud triangles), type 5 (lattice-form),
///     and types 6/7 (Coons / tensor-product patch meshes) — the latter approximated by two
///     triangles per patch from the four corner points and colours, which captures the
///     dominant colour blend without evaluating the cubic patch surface.
/// </summary>
internal static class MeshShadingDecoder
{
    internal static List<ShadingTriangle> Decode(PdfStream stream, PdfDocumentCore core, int shadingType)
    {
        var triangles = new List<ShadingTriangle>();
        try
        {
            var dict = stream.Dictionary;
            var bpc = (int)(dict.Get<PdfInteger>(PdfName.BitsPerCoordinate)?.Value ?? 0);
            var bpComp = (int)(dict.Get<PdfInteger>(PdfName.BitsPerComponent)?.Value ?? 0);
            var bpf = (int)(dict.Get<PdfInteger>(PdfName.BitsPerFlag)?.Value ?? 0);
            var decode = ReadDoubles(dict["Decode"]);
            if (bpc == 0 || bpComp == 0 || decode is null || decode.Length < 6)
                return triangles;

            var vpr = (int)(dict.Get<PdfInteger>(PdfName.VerticesPerRow)?.Value ?? 0);
            var fn = PdfFunction.Build(dict["Function"], core);
            // Colour component count: 1 when a /Function is present (single parametric input),
            // else the colour-space channel count inferred from the Decode array length.
            var nComp = fn is not null ? 1 : Math.Max(1, (decode.Length - 4) / 2);

            var data = StreamFilters.Decode(stream).Span;
            var reader = new BitCursor(data);

            switch (shadingType)
            {
                case 4:
                    DecodeType4(
                        reader,
                        bpf,
                        bpc,
                        bpComp,
                        decode,
                        nComp,
                        fn,
                        triangles
                    ); break;
                case 5:
                    DecodeType5(
                        reader,
                        bpc,
                        bpComp,
                        decode,
                        nComp,
                        fn,
                        vpr,
                        triangles
                    ); break;
                case 6:
                    DecodePatches(
                        reader,
                        bpf,
                        bpc,
                        bpComp,
                        decode,
                        nComp,
                        fn,
                        12,
                        triangles
                    ); break;
                case 7:
                    DecodePatches(
                        reader,
                        bpf,
                        bpc,
                        bpComp,
                        decode,
                        nComp,
                        fn,
                        16,
                        triangles
                    ); break;
            }
        }
        catch
        {
            // Malformed mesh data — return whatever decoded successfully (possibly empty).
        }

        return triangles;
    }

    // ── Type 4: free-form Gouraud triangles ─────────────────────────────────────
    private static void DecodeType4(
        BitCursor r,
        int bpf,
        int bpc,
        int bpComp,
        IReadOnlyList<double> decode,
        int nComp,
        PdfFunction? fn,
        List<ShadingTriangle> tris
    )
    {
        Vertex? va = null, vb = null;
        while (r.HasBits(bpf + (2 * bpc) + (nComp * bpComp)))
        {
            var flag = (int)r.Read(bpf);
            var v = ReadVertex(
                r,
                bpc,
                bpComp,
                decode,
                nComp,
                fn
            );
            switch (flag)
            {
                case 0:
                    // Start of a new triangle: read two more vertices.
                    if (!r.HasBits(2 * (bpf + (2 * bpc) + (nComp * bpComp))))
                        return;

                    r.Read(bpf);
                    var v1 = ReadVertex(
                        r,
                        bpc,
                        bpComp,
                        decode,
                        nComp,
                        fn
                    );
                    r.Read(bpf);
                    var v2 = ReadVertex(
                        r,
                        bpc,
                        bpComp,
                        decode,
                        nComp,
                        fn
                    );
                    tris.Add(Tri(v, v1, v2));
                    va = v1;
                    vb = v2;
                break;
                case 1: // share vertices vb, v(prev c) — use (vb, prev, v)
                    if (va is { } a1 && vb is { } b1)
                    {
                        tris.Add(Tri(b1, GetPrev(tris), v));
                        va = b1;
                        vb = v;
                        _ = a1;
                    }

                break;
                case 2:
                    if (va is { } a2 && vb is { } b2)
                    {
                        tris.Add(Tri(a2, b2, v));
                        vb = v;
                    }

                break;
            }

            r.AlignToByte(); // each vertex record is byte-aligned in type 4
        }
    }

    private static Vertex GetPrev(List<ShadingTriangle> tris)
    {
        var t = tris[^1];
        return new Vertex(t.X2, t.Y2, t.R2, t.G2, t.B2);
    }

    // ── Type 5: lattice-form Gouraud ────────────────────────────────────────────
    private static void DecodeType5(
        BitCursor r,
        int bpc,
        int bpComp,
        IReadOnlyList<double> decode,
        int nComp,
        PdfFunction? fn,
        int vpr,
        ICollection<ShadingTriangle> tris
    )
    {
        if (vpr < 2)
            return;

        var rows = new List<Vertex[]>();
        while (true)
        {
            var row = new Vertex[vpr];
            var ok = true;
            for (var i = 0; i < vpr; i++)
            {
                if (!r.HasBits((2 * bpc) + (nComp * bpComp)))
                {
                    ok = false;
                    break;
                }

                row[i] = ReadVertex(
                    r,
                    bpc,
                    bpComp,
                    decode,
                    nComp,
                    fn
                );
            }

            if (!ok)
                break;

            rows.Add(row);
        }

        for (var rr = 0; rr + 1 < rows.Count; rr++)
        for (var cc = 0; cc + 1 < vpr; cc++)
        {
            var a = rows[rr][cc];
            var b = rows[rr][cc + 1];
            var c = rows[rr + 1][cc];
            var d = rows[rr + 1][cc + 1];
            tris.Add(Tri(a, b, c));
            tris.Add(Tri(b, d, c));
        }
    }

    // ── Types 6/7: Coons / tensor patches → 2 triangles per patch from corners ──
    private static void DecodePatches(
        BitCursor r,
        int bpf,
        int bpc,
        int bpComp,
        IReadOnlyList<double> decode,
        int nComp,
        PdfFunction? fn,
        int controlPoints,
        ICollection<ShadingTriangle> tris
    )
    {
        // Track the previous patch's corners/colours for flag-based edge sharing (we read
        // them but, for the triangle approximation, always use the 4 corners of each patch).
        while (r.HasBits(bpf))
        {
            var flag = (int)r.Read(bpf);
            var newPoints = flag == 0 ? controlPoints : controlPoints - 4;
            var newColors = flag == 0 ? 4 : 2;

            var pts = new (double X, double Y)[newPoints];
            for (var i = 0; i < newPoints; i++)
            {
                if (!r.HasBits(2 * bpc))
                    return;

                pts[i] = ReadPoint(r, bpc, decode);
            }

            var cols = new (byte R, byte G, byte B)[newColors];
            for (var i = 0; i < newColors; i++)
            {
                if (!r.HasBits(nComp * bpComp))
                    return;

                cols[i] = ReadColor(r, bpComp, decode, nComp, fn);
            }

            // For a fresh patch (flag 0) the first 4 control points are the corner curve
            // starts at indices 0, 3, 6, 9 (Coons/tensor share this corner ordering).
            if (flag == 0 && newPoints >= 10)
            {
                var c0 = pts[0];
                var c1 = pts[3];
                var c2 = pts[6];
                var c3 = pts[9];
                var a = new Vertex(c0.X, c0.Y, cols[0].R, cols[0].G, cols[0].B);
                var b = new Vertex(c1.X, c1.Y, cols[1].R, cols[1].G, cols[1].B);
                var c = new Vertex(c2.X, c2.Y, cols[2].R, cols[2].G, cols[2].B);
                var d = new Vertex(c3.X, c3.Y, cols[3].R, cols[3].G, cols[3].B);
                tris.Add(Tri(a, b, c));
                tris.Add(Tri(a, c, d));
            }

            r.AlignToByte();
        }
    }

    private static Vertex ReadVertex(
        BitCursor r,
        int bpc,
        int bpComp,
        IReadOnlyList<double> decode,
        int nComp,
        PdfFunction? fn
    )
    {
        var (x, y) = ReadPoint(r, bpc, decode);
        var (cr, cg, cb) = ReadColor(r, bpComp, decode, nComp, fn);
        return new Vertex(x, y, cr, cg, cb);
    }

    private static (double X, double Y) ReadPoint(BitCursor r, int bpc, IReadOnlyList<double> decode)
    {
        var max = (1UL << bpc) - 1;
        var xr = r.Read(bpc) / (double)max;
        var yr = r.Read(bpc) / (double)max;
        var x = decode[0] + (xr * (decode[1] - decode[0]));
        var y = decode[2] + (yr * (decode[3] - decode[2]));

        return (x, y);
    }

    private static (byte R, byte G, byte B) ReadColor(
        BitCursor r,
        int bpComp,
        IReadOnlyList<double> decode,
        int nComp,
        PdfFunction? fn
    )
    {
        var max = (1UL << bpComp) - 1;
        var comps = new double[nComp];
        for (var i = 0; i < nComp; i++)
        {
            var raw = r.Read(bpComp) / (double)max;
            var lo = decode[4 + (2 * i)];
            var hi = decode[5 + (2 * i)];
            comps[i] = lo + (raw * (hi - lo));
        }

        if (fn is not null)
            comps = fn.Eval(comps[0]);
        return ComponentsToRgb(comps);
    }

    private static (byte R, byte G, byte B) ComponentsToRgb(IReadOnlyList<double> c)
    {
        return c.Count switch
        {
            >= 4 => CmykToBytes(c[0], c[1], c[2], c[3]),
            3 => (B255(c[0]), B255(c[1]), B255(c[2])),
            1 => (B255(c[0]), B255(c[0]), B255(c[0])),
            _ => (128, 128, 128)
        };

        static byte B255(double v) => ColorMath.ToByteRounded(v);

        // ReSharper disable once BadListLineBreaks
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

    private static ShadingTriangle Tri(Vertex a, Vertex b, Vertex c) =>
        new(
            a.X,
            a.Y,
            a.R,
            a.G,
            a.B,
            b.X,
            b.Y,
            b.R,
            b.G,
            b.B,
            c.X,
            c.Y,
            c.R,
            c.G,
            c.B
        );

    private static double[]? ReadDoubles(PdfObject? obj) => obj is PdfArray a
        ? a.Elements.Select(static e => e.ReadIntOrReal()).ToArray()
        : null;

    // ── Vertex / point / colour readers ─────────────────────────────────────────
    private readonly record struct Vertex(double X,
        double Y,
        byte R,
        byte G,
        byte B
    );

    // MSB-first bit cursor over a byte span.
    private sealed class BitCursor(ReadOnlySpan<byte> data)
    {
        private readonly byte[] _data = data.ToArray();
        private int _bit;

        public bool HasBits(int n) => _bit + n <= _data.Length * 8;

        public ulong Read(int count)
        {
            ulong v = 0;
            for (var i = 0; i < count; i++)
            {
                var byteIdx = _bit >> 3;
                var bit = byteIdx < _data.Length ? _data[byteIdx].BitMsbFirst(_bit) : 0;
                v = (v << 1) | (uint)bit;
                _bit++;
            }

            return v;
        }

        public void AlignToByte() => _bit = (_bit + 7) & ~7;
    }
}
