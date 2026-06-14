using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Rendering;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Rendering;

/// <summary>
///     Unit tests for <see cref="ShadingMath" /> — the pure, state-free gradient mathematics
///     (axial/radial parametric coordinate and the device→user affine inverse).
/// </summary>
public sealed class ShadingMathTests
{
    private static ShadingInfo Axial(bool extendStart = false, bool extendEnd = false) =>
        new(2, [0, 0, 10, 0], extendStart, extendEnd, new byte[256 * 3]);

    private static ShadingInfo Radial(bool extendStart = false, bool extendEnd = false) =>
        new(3, [0, 0, 0, 0, 0, 10], extendStart, extendEnd, new byte[256 * 3]);

    [Fact]
    public void ShadingT_Axial_Start_ReturnsZero()
    {
        ShadingMath.ShadingT(Axial(), 0, 0, out var t).ShouldBeTrue();
        t.ShouldBe(0.0, 0.0001);
    }

    [Fact]
    public void ShadingT_Axial_End_ReturnsOne()
    {
        ShadingMath.ShadingT(Axial(), 10, 0, out var t).ShouldBeTrue();
        t.ShouldBe(1.0, 0.0001);
    }

    [Fact]
    public void ShadingT_Axial_Midpoint_ReturnsHalf()
    {
        ShadingMath.ShadingT(Axial(), 5, 0, out var t).ShouldBeTrue();
        t.ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public void ShadingT_Axial_BeforeStart_NoExtend_ReturnsFalse() =>
        ShadingMath.ShadingT(Axial(), -5, 0, out _).ShouldBeFalse();

    [Fact]
    public void ShadingT_Axial_BeforeStart_WithExtend_ClampsToZero()
    {
        ShadingMath.ShadingT(Axial(true), -5, 0, out var t).ShouldBeTrue();
        t.ShouldBe(0.0);
    }

    [Fact]
    public void ShadingT_Axial_PastEnd_NoExtend_ReturnsFalse() =>
        ShadingMath.ShadingT(Axial(), 20, 0, out _).ShouldBeFalse();

    [Fact]
    public void ShadingT_Axial_PastEnd_WithExtend_ClampsToOne()
    {
        ShadingMath.ShadingT(Axial(extendEnd: true), 20, 0, out var t).ShouldBeTrue();
        t.ShouldBe(1.0);
    }

    [Fact]
    public void ShadingT_Axial_DegenerateAxis_ReturnsZero()
    {
        var sh = new ShadingInfo(2, [5, 5, 5, 5], false, false, new byte[256 * 3]);
        ShadingMath.ShadingT(sh, 5, 5, out var t).ShouldBeTrue();
        t.ShouldBe(0.0);
    }

    [Fact]
    public void ShadingT_Radial_AtOuterRadius_ReturnsOne()
    {
        ShadingMath.ShadingT(Radial(), 10, 0, out var t).ShouldBeTrue();
        t.ShouldBe(1.0, 0.0001);
    }

    [Fact]
    public void ShadingT_Radial_AtCentre_ReturnsZero()
    {
        ShadingMath.ShadingT(Radial(), 0, 0, out var t).ShouldBeTrue();
        t.ShouldBe(0.0, 0.0001);
    }

    [Fact]
    public void TryInvertDeviceToUser_Identity_RoundTrips()
    {
        double[] ctm = [1, 0, 0, 1, 0, 0];
        ShadingMath.TryInvertDeviceToUser(ctm, 1.0, 100, out var inv).ShouldBeTrue();

        // Forward maps user (x,y) → device (x, 100-y). Inverse should undo it.
        var (ux, uy) = ShadingMath.ApplyInv(inv, 30, 40);
        ux.ShouldBe(30, 0.0001);
        uy.ShouldBe(60, 0.0001); // 100 - 40
    }

    [Fact]
    public void TryInvertDeviceToUser_Singular_ReturnsFalse()
    {
        double[] ctm = [0, 0, 0, 0, 0, 0];
        ShadingMath.TryInvertDeviceToUser(ctm, 1.0, 100, out var inv).ShouldBeFalse();
        inv.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyInv_AppliesAffine()
    {
        // inv = [a b c d e f] → (a*px + c*py + e, b*px + d*py + f)
        double[] m = [2, 0, 0, 3, 1, 1];
        var (x, y) = ShadingMath.ApplyInv(m, 10, 10);
        x.ShouldBe(21, 0.0001); // 2*10 + 0 + 1
        y.ShouldBe(31, 0.0001); // 3*10 + 0 + 1
    }
}
