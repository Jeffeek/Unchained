using Shouldly;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Content;

/// <summary>
///     Additional <see cref="MeshShadingDecoder" /> tests covering Coons/tensor patch meshes
///     (types 6/7), the parametric <c>/Function</c> colour path, and CMYK colour components —
///     branches the base <see cref="MeshShadingDecoderTests" /> does not reach.
/// </summary>
public sealed class MeshShadingDecoderPatchTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    // Decode array for [x y rgb]: x/y mapped 0..100, colour 0..1.
    private static PdfArray RgbDecode() => new(
        new List<PdfObject>
        {
            new PdfInteger(0), new PdfInteger(100),
            new PdfInteger(0), new PdfInteger(100),
            new PdfInteger(0), new PdfInteger(1),
            new PdfInteger(0), new PdfInteger(1),
            new PdfInteger(0), new PdfInteger(1)
        }
    );

    private static PdfStream PatchStream(int shadingType, byte[] data)
    {
        var entries = new Dictionary<string, PdfObject>
        {
            ["ShadingType"] = new PdfInteger(shadingType),
            ["BitsPerCoordinate"] = new PdfInteger(8),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["BitsPerFlag"] = new PdfInteger(8),
            ["Decode"] = RgbDecode()
        };
        return new PdfStream(new PdfDictionary(entries), data);
    }

    [Fact]
    public void Type6_SingleCoonsPatch_DecodesTwoTriangles()
    {
        // flag(1) + 12 control points (24 bytes) + 4 colours (12 bytes) = 37 bytes.
        var data = new byte[1 + (12 * 2) + (4 * 3)];
        // flag = 0 (new patch). Coordinates/colours can stay 0; corners 0,3,6,9 differ via index.
        // Set distinct coordinates so the two triangles are non-degenerate.
        data[0] = 0;
        // Point 0 → (0,0); point 3 → (100,0); point 6 → (100,100); point 9 → (0,100).
        // Each point is 2 bytes (x,y). Point i occupies offset 1 + i*2.
        data[1 + (0 * 2)] = 0;
        data[1 + (0 * 2) + 1] = 0;
        data[1 + (3 * 2)] = 255;
        data[1 + (3 * 2) + 1] = 0;
        data[1 + (6 * 2)] = 255;
        data[1 + (6 * 2) + 1] = 255;
        data[1 + (9 * 2)] = 0;
        data[1 + (9 * 2) + 1] = 255;
        // Colours start after the 24 coordinate bytes: offset 25.
        const int colBase = 1 + (12 * 2);
        data[colBase] = 255;         // c0 R
        data[colBase + 3] = 255;     // c1 G
        data[colBase + 6 + 2] = 255; // c2 B

        var tris = MeshShadingDecoder.Decode(PatchStream(6, data), Core(), 6);
        tris.Count.ShouldBe(2);
    }

    [Fact]
    public void Type7_SingleTensorPatch_DecodesTwoTriangles()
    {
        // flag(1) + 16 control points (32 bytes) + 4 colours (12 bytes).
        var data = new byte[1 + (16 * 2) + (4 * 3)];
        data[0] = 0;
        data[1 + (3 * 2)] = 255; // give a couple corners non-zero coords
        data[1 + (6 * 2)] = 255;
        data[1 + (6 * 2) + 1] = 255;
        data[1 + (9 * 2) + 1] = 255;

        var tris = MeshShadingDecoder.Decode(PatchStream(7, data), Core(), 7);
        tris.Count.ShouldBe(2);
    }

    [Fact]
    public void Type6_EmptyData_ReturnsEmpty() =>
        MeshShadingDecoder.Decode(PatchStream(6, []), Core(), 6).ShouldBeEmpty();

    [Fact]
    public void Type4_WithFunction_UsesSingleParametricComponent()
    {
        // Decode array with a single colour component (parametric t): [x y t].
        var decode = new PdfArray(
            new List<PdfObject>
            {
                new PdfInteger(0), new PdfInteger(100),
                new PdfInteger(0), new PdfInteger(100),
                new PdfInteger(0), new PdfInteger(1)
            }
        );
        var fn = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                ["C0"] = new PdfArray([new PdfReal(0), new PdfReal(0), new PdfReal(0)]),
                ["C1"] = new PdfArray([new PdfReal(1), new PdfReal(1), new PdfReal(1)]),
                ["N"] = new PdfReal(1.0)
            }
        );
        var entries = new Dictionary<string, PdfObject>
        {
            ["ShadingType"] = new PdfInteger(4),
            ["BitsPerCoordinate"] = new PdfInteger(8),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["BitsPerFlag"] = new PdfInteger(8),
            ["Decode"] = decode,
            ["Function"] = fn
        };
        // 3 vertices, each: flag(1) + x(1) + y(1) + t(1) = 4 bytes.
        byte[] data =
        [
            0, 0, 0, 0,
            0, 100, 0, 128,
            0, 0, 100, 255
        ];
        var stream = new PdfStream(new PdfDictionary(entries), data);

        var tris = MeshShadingDecoder.Decode(stream, Core(), 4);
        tris.Count.ShouldBe(1);
        // t=0 → black vertex, t=255/255=1 → white vertex.
        tris[0].R0.ShouldBeLessThan((byte)20);
        tris[0].R2.ShouldBeGreaterThan((byte)235);
    }

    [Fact]
    public void Type4_CmykColors_ConvertsViaCmyk()
    {
        // Decode array for 4 colour channels (CMYK): [x y c m y k].
        var decode = new PdfArray(
            new List<PdfObject>
            {
                new PdfInteger(0), new PdfInteger(100),
                new PdfInteger(0), new PdfInteger(100),
                new PdfInteger(0), new PdfInteger(1),
                new PdfInteger(0), new PdfInteger(1),
                new PdfInteger(0), new PdfInteger(1),
                new PdfInteger(0), new PdfInteger(1)
            }
        );
        var entries = new Dictionary<string, PdfObject>
        {
            ["ShadingType"] = new PdfInteger(4),
            ["BitsPerCoordinate"] = new PdfInteger(8),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["BitsPerFlag"] = new PdfInteger(8),
            ["Decode"] = decode
        };
        // 3 vertices, each: flag(1)+x(1)+y(1)+c(1)+m(1)+y(1)+k(1) = 7 bytes.
        // All-zero CMYK → white.
        byte[] data =
        [
            0, 0, 0, 0, 0, 0, 0,
            0, 100, 0, 0, 0, 0, 0,
            0, 0, 100, 0, 0, 0, 0
        ];
        var stream = new PdfStream(new PdfDictionary(entries), data);

        var tris = MeshShadingDecoder.Decode(stream, Core(), 4);
        tris.Count.ShouldBe(1);
        tris[0].R0.ShouldBe((byte)255); // CMYK 0,0,0,0 = white
    }
}
