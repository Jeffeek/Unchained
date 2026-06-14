using Shouldly;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests;

/// <summary>
///     Covers the <see cref="Emu" /> conversion-back, comparison, and formatting members not
///     exercised by <see cref="EmuTests" /> (which focuses on the From* factories and arithmetic).
/// </summary>
public sealed class EmuConversionTests
{
    [Fact]
    public void ToCentimetres_Converts() =>
        new Emu(360_000).ToCentimetres().ShouldBe(1.0, 0.0001);

    [Fact]
    public void ToPoints_Converts() =>
        new Emu(12_700).ToPoints().ShouldBe(1.0, 0.0001);

    [Fact]
    public void ToPixels_At96Dpi_Converts() =>
        new Emu(914_400).ToPixels(96).ShouldBe(96.0, 0.0001);

    [Fact]
    public void Multiply_ScalarOnLeft_Works() =>
        (2.0 * new Emu(100)).Value.ShouldBe(200);

    [Fact]
    public void LessThan_Works() =>
        (new Emu(100) < new Emu(200)).ShouldBeTrue();

    [Fact]
    public void GreaterThan_Works() =>
        (new Emu(300) > new Emu(200)).ShouldBeTrue();

    [Fact]
    public void CompareTo_OrdersByValue()
    {
        new Emu(100).CompareTo(new Emu(200)).ShouldBeLessThan(0);
        new Emu(200).CompareTo(new Emu(100)).ShouldBeGreaterThan(0);
        new Emu(100).CompareTo(new Emu(100)).ShouldBe(0);
    }

    [Fact]
    public void GetHashCode_EqualValues_Match() =>
        new Emu(123).GetHashCode().ShouldBe(new Emu(123).GetHashCode());

    [Fact]
    public void Equals_BoxedObject_Works()
    {
        object boxed = new Emu(55);
        new Emu(55).Equals(boxed).ShouldBeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global
        new Emu(55).Equals("not an emu").ShouldBeFalse();
    }

    [Fact]
    public void ToString_ContainsValueAndUnit() =>
        new Emu(914_400).ToString().ShouldBe("914400 EMU");
}
