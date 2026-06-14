using Shouldly;
using Unchained.Ooxml.Drawing;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Drawing;

public sealed class EffectFormatTests
{
    [Fact]
    public void Default_IsEmpty_AllEffectsNull()
    {
        var effects = new EffectFormat();
        effects.IsEmpty.ShouldBeTrue();
        effects.OuterShadow.ShouldBeNull();
        effects.InnerShadow.ShouldBeNull();
        effects.Glow.ShouldBeNull();
        effects.Reflection.ShouldBeNull();
        effects.SoftEdge.ShouldBeNull();
        effects.Blur.ShouldBeNull();
    }

    [Fact]
    public void WithOuterShadow_IsNotEmpty()
    {
        var effects = new EffectFormat { OuterShadow = new OuterShadowEffect() };
        effects.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void WithBlur_IsNotEmpty()
    {
        var effects = new EffectFormat { Blur = new BlurEffect() };
        effects.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void OuterShadow_Defaults()
    {
        var shadow = new OuterShadowEffect();
        shadow.Color.ShouldBe(ColorSpec.FromArgb(0x80, 0, 0, 0));
        shadow.ScaleHorizontalPercent.ShouldBe(100);
        shadow.ScaleVerticalPercent.ShouldBe(100);
        shadow.Alignment.ShouldBe("tl");
        shadow.RotateWithShape.ShouldBeFalse();
    }

    [Fact]
    public void OuterShadow_Properties_RoundTrip()
    {
        var shadow = new OuterShadowEffect
        {
            BlurRadius = Emu.FromPoints(3),
            Distance = Emu.FromPoints(4),
            DirectionDegrees = 45,
            ScaleHorizontalPercent = 90,
            ScaleVerticalPercent = 80,
            Alignment = "br",
            RotateWithShape = true
        };
        shadow.BlurRadius.ShouldBe(Emu.FromPoints(3));
        shadow.Distance.ShouldBe(Emu.FromPoints(4));
        shadow.DirectionDegrees.ShouldBe(45);
        shadow.ScaleHorizontalPercent.ShouldBe(90);
        shadow.ScaleVerticalPercent.ShouldBe(80);
        shadow.Alignment.ShouldBe("br");
        shadow.RotateWithShape.ShouldBeTrue();
    }

    [Fact]
    public void InnerShadow_Defaults()
    {
        var shadow = new InnerShadowEffect();
        shadow.Color.ShouldBe(ColorSpec.FromArgb(0x80, 0, 0, 0));
        shadow.DirectionDegrees.ShouldBe(0);
    }

    [Fact]
    public void Glow_DefaultColorIsYellow()
    {
        var glow = new GlowEffect();
        glow.Color.ShouldBe(ColorSpec.FromRgb(0xFF, 0xFF, 0x00));
    }

    [Fact]
    public void Reflection_Defaults()
    {
        var reflection = new ReflectionEffect();
        reflection.StartOpacityPercent.ShouldBe(100);
        reflection.EndOpacityPercent.ShouldBe(0);
    }

    [Fact]
    public void SoftEdge_RadiusRoundTrips()
    {
        var soft = new SoftEdgeEffect { Radius = Emu.FromPoints(2) };
        soft.Radius.ShouldBe(Emu.FromPoints(2));
    }

    [Fact]
    public void Blur_DefaultGrowBoundsTrue()
    {
        var blur = new BlurEffect();
        blur.GrowBounds.ShouldBeTrue();
    }
}
