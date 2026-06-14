using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class ShadingInfoTests
{
    private static byte[] Ramp()
    {
        // 256 RGB triples: a black→white grey ramp where each channel == index.
        var ramp = new byte[256 * 3];
        for (var i = 0; i < 256; i++)
        {
            ramp[(i * 3) + 0] = (byte)i;
            ramp[(i * 3) + 1] = (byte)i;
            ramp[(i * 3) + 2] = (byte)i;
        }

        return ramp;
    }

    [Fact]
    public void Axial_IsMesh_False()
    {
        var info = new ShadingInfo(2, [0, 0, 1, 1], false, false, Ramp());
        info.IsMesh.ShouldBeFalse();
    }

    [
        Theory,
        InlineData(4),
        InlineData(5),
        InlineData(6),
        InlineData(7)
    ]
    public void MeshTypes_IsMesh_True(int shadingType)
    {
        var info = new ShadingInfo(shadingType, [], false, false, [], []);
        info.IsMesh.ShouldBeTrue();
    }

    [Fact]
    public void ColorAt_Zero_ReturnsFirstRampEntry()
    {
        var info = new ShadingInfo(2, [0, 0, 1, 1], false, false, Ramp());
        info.ColorAt(0.0).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void ColorAt_One_ReturnsLastRampEntry()
    {
        var info = new ShadingInfo(2, [0, 0, 1, 1], false, false, Ramp());
        info.ColorAt(1.0).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void ColorAt_Half_ReturnsMiddleEntry()
    {
        var info = new ShadingInfo(3, [0, 0, 0, 0, 0, 1], true, true, Ramp());
        // Round(0.5 * 255) = 128.
        info.ColorAt(0.5).ShouldBe(((byte)128, (byte)128, (byte)128));
    }

    [Fact]
    public void ColorAt_OutOfRange_IsClamped()
    {
        var info = new ShadingInfo(2, [0, 0, 1, 1], false, false, Ramp());
        info.ColorAt(-1.0).ShouldBe(((byte)0, (byte)0, (byte)0));
        info.ColorAt(5.0).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void ExtendFlags_RoundTrip()
    {
        var info = new ShadingInfo(2, [0, 0, 1, 1], true, false, Ramp());
        info.ShadingType.ShouldBe(2);
        info.ExtendStart.ShouldBeTrue();
        info.ExtendEnd.ShouldBeFalse();
        info.Coords.ShouldBe([0, 0, 1, 1]);
    }
}

public sealed class ShadingTriangleTests
{
    [Fact]
    public void Constructor_StoresAllVertices()
    {
        var t = new ShadingTriangle(
            0, 0, 255, 0, 0,
            10, 0, 0, 255, 0,
            5, 10, 0, 0, 255);

        t.X0.ShouldBe(0);
        t.Y2.ShouldBe(10);
        t.R0.ShouldBe((byte)255);
        t.G1.ShouldBe((byte)255);
        t.B2.ShouldBe((byte)255);
    }

    [Fact]
    public void RecordEquality_SameVertices_AreEqual()
    {
        var a = new ShadingTriangle(0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13);
        var b = new ShadingTriangle(0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13);
        a.ShouldBe(b);
    }
}
