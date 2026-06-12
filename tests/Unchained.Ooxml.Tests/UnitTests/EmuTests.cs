using Shouldly;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests;

public sealed class EmuTests
{
    [
        Theory,
        InlineData(1.0, 914_400),
        InlineData(0.5, 457_200),
        InlineData(2.54, 2_322_576)
    ]
    public void FromInches_ConvertsCorrectly(double inches, long expectedEmu)
    {
        var emu = Emu.FromInches(inches);
        emu.Value.ShouldBe(expectedEmu);
    }

    [
        Theory,
        InlineData(1.0, 360_000),
        InlineData(2.0, 720_000)
    ]
    public void FromCentimetres_ConvertsCorrectly(double cm, long expectedEmu)
    {
        var emu = Emu.FromCentimetres(cm);
        emu.Value.ShouldBe(expectedEmu);
    }

    [Fact]
    public void FromPoints_ConvertsCorrectly()
    {
        var emu = Emu.FromPoints(72);
        emu.Value.ShouldBe(914_400);
    }

    [Fact]
    public void FromPixels_At96Dpi_ConvertsCorrectly()
    {
        var emu = Emu.FromPixels(96, 96);
        emu.Value.ShouldBe(914_400);
    }

    [Fact]
    public void ToInches_RoundTrips()
    {
        var original = Emu.FromInches(3.0);
        original.ToInches().ShouldBe(3.0, 0.0001);
    }

    [Fact]
    public void Addition_WorksCorrectly()
    {
        var a = new Emu(100);
        var b = new Emu(200);
        (a + b).Value.ShouldBe(300);
    }

    [Fact]
    public void Subtraction_WorksCorrectly()
    {
        var a = new Emu(500);
        var b = new Emu(200);
        (a - b).Value.ShouldBe(300);
    }

    [Fact]
    public void Multiplication_ByScalar_WorksCorrectly()
    {
        var emu = new Emu(100);
        (emu * 2.0).Value.ShouldBe(200);
    }

    [Fact]
    public void Equality_SameValue_ReturnsTrue()
    {
        var a = new Emu(12345);
        var b = new Emu(12345);
        (a == b).ShouldBeTrue();
        a.Equals(b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentValue_ReturnsFalse()
    {
        var a = new Emu(100);
        var b = new Emu(200);
        (a == b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Zero_HasValueZero() => Emu.Zero.Value.ShouldBe(0);
}
