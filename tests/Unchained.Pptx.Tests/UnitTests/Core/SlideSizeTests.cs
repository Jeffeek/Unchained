using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Core;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Core;

public sealed class SlideSizeTests
{
    [Fact]
    public void Constructor_StoresDimensions()
    {
        var size = new SlideSize(new Emu(100), new Emu(200));
        size.Width.ShouldBe(new Emu(100));
        size.Height.ShouldBe(new Emu(200));
    }

    [Fact]
    public void Widescreen_Is16By9Dimensions()
    {
        SlideSize.Widescreen.Width.Value.ShouldBe(12_192_000);
        SlideSize.Widescreen.Height.Value.ShouldBe(6_858_000);
    }

    [Fact]
    public void Standard_Is4By3Dimensions()
    {
        SlideSize.Standard.Width.Value.ShouldBe(9_144_000);
        SlideSize.Standard.Height.Value.ShouldBe(6_858_000);
    }

    [Fact]
    public void Custom_CreatesArbitrarySize()
    {
        var size = SlideSize.Custom(new Emu(5), new Emu(7));
        size.Width.Value.ShouldBe(5);
        size.Height.Value.ShouldBe(7);
    }

    [Fact]
    public void Equality_SameDimensions_AreEqual()
    {
        var a = new SlideSize(new Emu(10), new Emu(20));
        var b = new SlideSize(new Emu(10), new Emu(20));
        (a == b).ShouldBeTrue();
        a.Equals(b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentDimensions_AreNotEqual()
    {
        var a = new SlideSize(new Emu(10), new Emu(20));
        var b = new SlideSize(new Emu(10), new Emu(21));
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void ToString_ContainsInchDimensions() =>
        SlideSize.Widescreen.ToString().ShouldContain("\"");
}
