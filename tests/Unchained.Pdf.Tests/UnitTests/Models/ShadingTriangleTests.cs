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
    public void AllFifteenVertexProperties_AreReadable()
    {
        // Reads every positional record property so each generated getter is exercised.
        var t = new ShadingTriangle(
            1.5,
            2.5,
            10,
            20,
            30,
            3.5,
            4.5,
            40,
            50,
            60,
            5.5,
            6.5,
            70,
            80,
            90
        );

        t.X0.ShouldBe(1.5);
        t.Y0.ShouldBe(2.5);
        t.R0.ShouldBe((byte)10);
        t.G0.ShouldBe((byte)20);
        t.B0.ShouldBe((byte)30);
        t.X1.ShouldBe(3.5);
        t.Y1.ShouldBe(4.5);
        t.R1.ShouldBe((byte)40);
        t.G1.ShouldBe((byte)50);
        t.B1.ShouldBe((byte)60);
        t.X2.ShouldBe(5.5);
        t.Y2.ShouldBe(6.5);
        t.R2.ShouldBe((byte)70);
        t.G2.ShouldBe((byte)80);
        t.B2.ShouldBe((byte)90);
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
