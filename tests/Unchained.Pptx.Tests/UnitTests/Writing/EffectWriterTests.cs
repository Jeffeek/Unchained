using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

public sealed class EffectWriterTests
{
    private static readonly XNamespace A = DmlNames.Dml;

    [Fact]
    public void Write_EmptyEffects_ReturnsNull() =>
        EffectWriter.Write(new EffectFormat()).ShouldBeNull();

    [Fact]
    public void Write_Blur_EmitsBlurElement()
    {
        var effects = new EffectFormat { Blur = new BlurEffect { Radius = Emu.FromPoints(4), GrowBounds = false } };
        var lst = EffectWriter.Write(effects);
        lst.ShouldNotBeNull();
        var blur = lst.Element(A + "blur");
        blur.ShouldNotBeNull();
        blur.Attribute("rad")!.Value.ShouldBe(Emu.FromPoints(4).Value.ToString());
        blur.Attribute("grow")!.Value.ShouldBe("0");
    }

    [Fact]
    public void Write_BlurGrowBounds_EmitsOne()
    {
        var effects = new EffectFormat { Blur = new BlurEffect { Radius = Emu.FromPoints(1) } };
        var blur = EffectWriter.Write(effects)!.Element(A + "blur");
        blur!.Attribute("grow")!.Value.ShouldBe("1");
    }

    [Fact]
    public void Write_Glow_EmitsGlowWithColor()
    {
        var effects = new EffectFormat
        {
            Glow = new GlowEffect { Radius = Emu.FromPoints(3), Color = ColorSpec.FromRgb(0xFF, 0x00, 0x00) }
        };
        var glow = EffectWriter.Write(effects)!.Element(A + "glow");
        glow.ShouldNotBeNull();
        glow.Attribute("rad")!.Value.ShouldBe(Emu.FromPoints(3).Value.ToString());
        glow.Elements().ShouldNotBeEmpty();
    }

    [Fact]
    public void Write_InnerShadow_EmitsInnerShdw()
    {
        var effects = new EffectFormat
        {
            InnerShadow = new InnerShadowEffect
            {
                BlurRadius = Emu.FromPoints(2),
                Distance = Emu.FromPoints(1),
                DirectionDegrees = 90
            }
        };
        var inner = EffectWriter.Write(effects)!.Element(A + "innerShdw");
        inner.ShouldNotBeNull();
        inner.Attribute("blurRad").ShouldNotBeNull();
        inner.Attribute("dist").ShouldNotBeNull();
        // 90 degrees * 60000 = 5400000.
        inner.Attribute("dir")!.Value.ShouldBe("5400000");
        inner.Elements().ShouldNotBeEmpty();
    }

    [Fact]
    public void Write_OuterShadow_EmitsOuterShdwWithScaleAndAlignment()
    {
        var effects = new EffectFormat
        {
            OuterShadow = new OuterShadowEffect
            {
                BlurRadius = Emu.FromPoints(2),
                Distance = Emu.FromPoints(3),
                DirectionDegrees = 45,
                ScaleHorizontalPercent = 100,
                ScaleVerticalPercent = 100,
                Alignment = "br",
                RotateWithShape = true
            }
        };
        var outer = EffectWriter.Write(effects)!.Element(A + "outerShdw");
        outer.ShouldNotBeNull();
        outer.Attribute("sx")!.Value.ShouldBe("100000");
        outer.Attribute("sy")!.Value.ShouldBe("100000");
        outer.Attribute("algn")!.Value.ShouldBe("br");
        outer.Attribute("rotWithShape")!.Value.ShouldBe("1");
    }

    [Fact]
    public void Write_Reflection_EmitsReflection()
    {
        var effects = new EffectFormat
        {
            Reflection = new ReflectionEffect
            {
                BlurRadius = Emu.FromPoints(1),
                StartOpacityPercent = 60,
                EndOpacityPercent = 0,
                Distance = Emu.FromPoints(2),
                DirectionDegrees = 90
            }
        };
        var refl = EffectWriter.Write(effects)!.Element(A + "reflection");
        refl.ShouldNotBeNull();
        refl.Attribute("stA")!.Value.ShouldBe("60000");
        refl.Attribute("endA")!.Value.ShouldBe("0");
    }

    [Fact]
    public void Write_SoftEdge_EmitsSoftEdge()
    {
        var effects = new EffectFormat { SoftEdge = new SoftEdgeEffect { Radius = Emu.FromPoints(5) } };
        var soft = EffectWriter.Write(effects)!.Element(A + "softEdge");
        soft.ShouldNotBeNull();
        soft.Attribute("rad")!.Value.ShouldBe(Emu.FromPoints(5).Value.ToString());
    }

    [Fact]
    public void Write_AllEffects_EmitsEveryChild()
    {
        var effects = new EffectFormat
        {
            Blur = new BlurEffect { Radius = Emu.FromPoints(1) },
            Glow = new GlowEffect { Radius = Emu.FromPoints(1) },
            InnerShadow = new InnerShadowEffect(),
            OuterShadow = new OuterShadowEffect(),
            Reflection = new ReflectionEffect(),
            SoftEdge = new SoftEdgeEffect()
        };
        var lst = EffectWriter.Write(effects);
        lst.ShouldNotBeNull();
        lst.Element(A + "blur").ShouldNotBeNull();
        lst.Element(A + "glow").ShouldNotBeNull();
        lst.Element(A + "innerShdw").ShouldNotBeNull();
        lst.Element(A + "outerShdw").ShouldNotBeNull();
        lst.Element(A + "reflection").ShouldNotBeNull();
        lst.Element(A + "softEdge").ShouldNotBeNull();
    }
}
