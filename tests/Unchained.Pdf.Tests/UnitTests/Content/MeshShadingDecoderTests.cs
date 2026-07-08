using Shouldly;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Content;

/// <summary>
///     Unit tests for <see cref="MeshShadingDecoder" /> — decodes type 4/5/6/7 mesh shadings into
///     Gouraud triangles. Streams are built with explicit bit-packed data (bpc/bpComp = 8 for
///     byte alignment, RGB colours, no /Function). A minimal single-page core satisfies the
///     signature; it is only dereferenced for an indirect /Function (none here).
/// </summary>
public sealed class MeshShadingDecoderTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    private static PdfArray DecodeArray() =>
        // [xMin xMax yMin yMax  rMin rMax  gMin gMax  bMin bMax]
        new(
            new List<PdfObject>
            {
                new PdfInteger(0), new PdfInteger(10),
                new PdfInteger(0), new PdfInteger(10),
                new PdfInteger(0), new PdfInteger(1),
                new PdfInteger(0), new PdfInteger(1),
                new PdfInteger(0), new PdfInteger(1)
            }
        );

    private static PdfArray DecodeArrayGrayscale() =>
        // [xMin xMax yMin yMax  vMin vMax]
        new(
            new List<PdfObject>
            {
                new PdfInteger(0), new PdfInteger(10),
                new PdfInteger(0), new PdfInteger(10),
                new PdfInteger(0), new PdfInteger(255)
            }
        );

    private static PdfStream MeshStream(int shadingType, byte[] data, int verticesPerRow = 0)
    {
        var entries = new Dictionary<string, PdfObject>
        {
            ["ShadingType"] = new PdfInteger(shadingType),
            ["BitsPerCoordinate"] = new PdfInteger(8),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["BitsPerFlag"] = new PdfInteger(8),
            ["Decode"] = DecodeArray()
        };
        if (verticesPerRow > 0)
            entries["VerticesPerRow"] = new PdfInteger(verticesPerRow);

        return new PdfStream(new PdfDictionary(entries), data);
    }

    private static PdfStream GrayscaleMeshStream(int shadingType, byte[] data, int verticesPerRow = 0)
    {
        var entries = new Dictionary<string, PdfObject>
        {
            ["ShadingType"] = new PdfInteger(shadingType),
            ["BitsPerCoordinate"] = new PdfInteger(8),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["BitsPerFlag"] = new PdfInteger(8),
            ["Decode"] = DecodeArrayGrayscale()
        };
        if (verticesPerRow > 0)
            entries["VerticesPerRow"] = new PdfInteger(verticesPerRow);

        return new PdfStream(new PdfDictionary(entries), data);
    }

    [Fact]
    public void Type4_SingleTriangle_DecodesOneTriangle()
    {
        // flag + (x,y) + (r,g,b) per vertex, all 8-bit. Three flag-0 vertices = one triangle.
        byte[] data =
        [
            0, 0, 0, 255, 0, 0,   // flag=0, v0 at (0,0) red
            0, 255, 0, 0, 255, 0, // flag, v1 at (10,0) green
            0, 0, 255, 0, 0, 255  // flag, v2 at (0,10) blue
        ];
        var triangles = MeshShadingDecoder.Decode(MeshStream(4, data), Core(), 4);

        triangles.Count.ShouldBe(1);
        var t = triangles[0];
        t.X0.ShouldBe(0, 0.001);
        t.X1.ShouldBe(10, 0.001);
        t.R0.ShouldBe((byte)255);
        t.G1.ShouldBe((byte)255);
        t.B2.ShouldBe((byte)255);
    }

    [Fact]
    public void Type5_Lattice2x2_DecodesTwoTriangles()
    {
        // 2 vertices-per-row, 2 rows = one quad = two triangles. No flags in type 5.
        byte[] data =
        [
            0, 0, 255, 0, 0,        // v(0,0) red
            255, 0, 0, 255, 0,      // v(10,0) green
            0, 255, 0, 0, 255,      // v(0,10) blue
            255, 255, 255, 255, 255 // v(10,10) white
        ];
        var triangles = MeshShadingDecoder.Decode(MeshStream(5, data, 2), Core(), 5);

        triangles.Count.ShouldBe(2);
    }

    [Fact]
    public void Type5_VerticesPerRowLessThanTwo_ReturnsEmpty()
    {
        byte[] data = [0, 0, 255, 0, 0];
        MeshShadingDecoder.Decode(MeshStream(5, data, 1), Core(), 5).ShouldBeEmpty();
    }

    [Fact]
    public void MissingBitsPerCoordinate_ReturnsEmpty()
    {
        var entries = new Dictionary<string, PdfObject>
        {
            ["BitsPerComponent"] = new PdfInteger(8),
            ["Decode"] = DecodeArray()
        };
        var stream = new PdfStream(new PdfDictionary(entries), new byte[] { 1, 2, 3 });
        MeshShadingDecoder.Decode(stream, Core(), 4).ShouldBeEmpty();
    }

    [Fact]
    public void ShortDecodeArray_ReturnsEmpty()
    {
        var entries = new Dictionary<string, PdfObject>
        {
            ["BitsPerCoordinate"] = new PdfInteger(8),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["Decode"] = new PdfArray(new List<PdfObject> { new PdfInteger(0), new PdfInteger(10) })
        };
        var stream = new PdfStream(new PdfDictionary(entries), new byte[] { 1, 2, 3 });
        MeshShadingDecoder.Decode(stream, Core(), 4).ShouldBeEmpty();
    }

    [Fact]
    public void Type4_EmptyData_ReturnsEmpty() =>
        MeshShadingDecoder.Decode(MeshStream(4, Array.Empty<byte>()), Core(), 4).ShouldBeEmpty();

    // ── Type4 fan tessellation (flags 1 and 2) ─────────────────────────────

    [Fact]
    public void Type4_Flag1_ShareVbWithPreviousTriangle()
    {
        // [flag=0, v0, v1, v2] then [flag=1, v3] → triangle(vb, GetPrev(tris), v).
        // Tri0: v0=(0,0) red, v1=(10,0) green, v2=(0,10) blue → va=v1=(10,0), vb=v2=(0,10)
        // Flag1: Tri(vb, GetPrev(tris)=v2, v3) = Tri((0,10), (0,10), (10,10))
        byte[] data =
        [
            0, 0, 0, 255, 0, 0,
            0, 255, 0, 0, 255, 0,
            0, 0, 255, 0, 0, 255,
            1, 255, 255, 255, 255, 255
        ];
        var triangles = MeshShadingDecoder.Decode(MeshStream(4, data), Core(), 4);
        triangles.Count.ShouldBe(2);

        var t1 = triangles[1];
        t1.X0.ShouldBe(0, 0.001); // vb.x
        t1.Y0.ShouldBe(10, 0.001);
        t1.X1.ShouldBe(0, 0.001); // GetPrev(tris) = v2.x
        t1.Y1.ShouldBe(10, 0.001);
        t1.X2.ShouldBe(10, 0.001); // v3.x
        t1.Y2.ShouldBe(10, 0.001);
        t1.R2.ShouldBe((byte)255);
        t1.G2.ShouldBe((byte)255);
        t1.B2.ShouldBe((byte)255);
    }

    [Fact]
    public void Type4_Flag2_ShareVaAndVbWithPreviousTriangle()
    {
        // [flag=0, v0, v1, v2] then [flag=2, v3] → triangle(va, vb, v).
        // Tri0: v0=(0,0) red, v1=(10,0) green, v2=(0,10) blue → va=v1=(10,0), vb=v2=(0,10)
        // Flag2: Tri(va, vb, v3) = Tri((10,0), (0,10), (10,10))
        byte[] data =
        [
            0, 0, 0, 255, 0, 0,
            0, 255, 0, 0, 255, 0,
            0, 0, 255, 0, 0, 255,
            2, 255, 255, 255, 255, 255
        ];
        var triangles = MeshShadingDecoder.Decode(MeshStream(4, data), Core(), 4);
        triangles.Count.ShouldBe(2);

        var t1 = triangles[1];
        t1.X0.ShouldBe(10, 0.001); // va.x
        t1.Y0.ShouldBe(0, 0.001);
        t1.X1.ShouldBe(0, 0.001); // vb.x
        t1.Y1.ShouldBe(10, 0.001);
        t1.X2.ShouldBe(10, 0.001); // v3.x
        t1.Y2.ShouldBe(10, 0.001);
        t1.R2.ShouldBe((byte)255);
        t1.G2.ShouldBe((byte)255);
        t1.B2.ShouldBe((byte)255);
    }

    [Fact]
    public void Type4_MixedFlags_FanOfThreeTriangles()
    {
        // [0,0] [0,1] [1,0] then [1,v3] then [2,v4]
        byte[] data =
        [
            // Triangle 0: v0=(0,0) red, v1=(10,0) green, v2=(0,10) blue
            0, 0, 0, 255, 0, 0,
            0, 255, 0, 0, 255, 0,
            0, 0, 255, 0, 0, 255,
            // Triangle 1: flag=1, v3=(10,10) yellow → Tri(v1, v2, v3)
            1, 255, 255, 0, 255, 255,
            // Triangle 2: flag=2, v4=(20,0) magenta → Tri(v3, v2, v4)
            2, 255, 0, 255, 255, 255
        ];
        var triangles = MeshShadingDecoder.Decode(MeshStream(4, data), Core(), 4);
        triangles.Count.ShouldBe(3);
    }

    // ── Grayscale decode ───────────────────────────────────────────────────

    [Fact]
    public void Type4_Grayscale_DecodesToOneTriangle()
    {
        byte[] data =
        [
            0, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 0
        ];
        var triangles = MeshShadingDecoder.Decode(GrayscaleMeshStream(4, data), Core(), 4);
        triangles.Count.ShouldBe(1);
        var t = triangles[0];
        t.X0.ShouldBe(0, 0.001);
        t.X1.ShouldBe(10, 0.001);
        // For grayscale: R=G=B=V (the single channel is replicated to all RGB components).
        t.R0.ShouldBe(t.G0);
        t.G0.ShouldBe(t.B0);
        t.R0.ShouldBe((byte)255);
        t.R1.ShouldBe((byte)255);
        t.R2.ShouldBe((byte)0);
    }

    // ── Type5 incomplete row ───────────────────────────────────────────────

    [Fact]
    public void Type5_IncompleteRow_PartialTriangle()
    {
        // 2 vertices-per-row, but second row has only 1 vertex (incomplete).
        // The incomplete row is discarded, leaving only 1 row → no triangles.
        byte[] data =
        [
            0, 0, 255, 0, 0,   // v(0,0) red
            255, 0, 0, 255, 0, // v(10,0) green
            0, 255, 0, 0, 255  // v(0,10) blue — no v(10,10)
        ];
        var triangles = MeshShadingDecoder.Decode(MeshStream(5, data, 2), Core(), 5);
        triangles.ShouldBeEmpty(); // incomplete second row → no triangles
    }
}
