using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class ShadingTriangleTests
{
    [Fact]
    public void Constructor_StoresAllVertices()
    {
        var t = new ShadingTriangle(
            0,
            0,
            255,
            0,
            0,
            10,
            0,
            0,
            255,
            0,
            5,
            10,
            0,
            0,
            255
        );

        t.X0.ShouldBe(0);
        t.Y2.ShouldBe(10);
        t.R0.ShouldBe((byte)255);
        t.G1.ShouldBe((byte)255);
        t.B2.ShouldBe((byte)255);
    }

    [Fact]
    public void RecordEquality_SameVertices_AreEqual()
    {
        // ReSharper disable BadListLineBreaks
        var a = new ShadingTriangle(
            0,
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13
        );
        var b = new ShadingTriangle(
            0,
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13
        );
        // ReSharper restore BadListLineBreaks
        a.ShouldBe(b);
    }
}
