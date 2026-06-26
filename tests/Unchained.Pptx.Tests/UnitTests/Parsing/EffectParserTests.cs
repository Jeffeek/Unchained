using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Parsing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

public sealed class EffectParserTests
{
    private static readonly XNamespace A = DmlNames.Dml;

    private static XElement WithEffects(params object?[] children) =>
        new(A + "spPr", new XElement(A + "effectLst", children));

    [Fact]
    public void Parse_NoEffectLst_LeavesEmpty()
    {
        var effects = new EffectFormat();
        EffectParser.Parse(new XElement(A + "spPr"), effects);
        effects.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Parse_NullParent_LeavesEmpty()
    {
        var effects = new EffectFormat();
        EffectParser.Parse(null, effects);
        effects.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Parse_OuterShadow_ReadsAttributes()
    {
        var outer = new XElement(
            A + "outerShdw",
            new XAttribute("blurRad", "50000"),
            new XAttribute("dist", "40000"),
            new XAttribute("dir", "2700000"),
            new XAttribute("sx", "90000"),
            new XAttribute("sy", "80000"),
            new XAttribute("algn", "br"),
            new XAttribute("rotWithShape", "1"),
            new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "808080"))
        );
        var effects = new EffectFormat();
        EffectParser.Parse(WithEffects(outer), effects);

        effects.OuterShadow.ShouldNotBeNull();
        effects.OuterShadow.BlurRadius.Value.ShouldBe(50_000);
        effects.OuterShadow.Distance.Value.ShouldBe(40_000);
        // 2,700,000 / 60,000 = 45 degrees.
        effects.OuterShadow.DirectionDegrees.ShouldBe(45.0, 0.01);
        effects.OuterShadow.ScaleHorizontalPercent.ShouldBe(90.0, 0.01);
        effects.OuterShadow.ScaleVerticalPercent.ShouldBe(80.0, 0.01);
        effects.OuterShadow.Alignment.ShouldBe("br");
        effects.OuterShadow.RotateWithShape.ShouldBeTrue();
    }

    [Fact]
    public void Parse_OuterShadow_DefaultsWhenAttributesAbsent()
    {
        var outer = new XElement(A + "outerShdw");
        var effects = new EffectFormat();
        EffectParser.Parse(WithEffects(outer), effects);

        effects.OuterShadow.ShouldNotBeNull();
        effects.OuterShadow.ScaleHorizontalPercent.ShouldBe(100.0);
        effects.OuterShadow.ScaleVerticalPercent.ShouldBe(100.0);
        effects.OuterShadow.Alignment.ShouldBe("tl");
        effects.OuterShadow.RotateWithShape.ShouldBeFalse();
    }

    [Fact]
    public void Parse_InnerShadow_ReadsAttributes()
    {
        var inner = new XElement(
            A + "innerShdw",
            new XAttribute("blurRad", "30000"),
            new XAttribute("dist", "20000"),
            new XAttribute("dir", "5400000"),
            new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "000000"))
        );
        var effects = new EffectFormat();
        EffectParser.Parse(WithEffects(inner), effects);

        effects.InnerShadow.ShouldNotBeNull();
        effects.InnerShadow.BlurRadius.Value.ShouldBe(30_000);
        effects.InnerShadow.DirectionDegrees.ShouldBe(90.0, 0.01);
    }

    [Fact]
    public void Parse_Glow_ReadsRadiusAndColor()
    {
        var glow = new XElement(
            A + "glow",
            new XAttribute("rad", "63500"),
            new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "FFFF00"))
        );
        var effects = new EffectFormat();
        EffectParser.Parse(WithEffects(glow), effects);

        effects.Glow.ShouldNotBeNull();
        effects.Glow.Radius.Value.ShouldBe(63_500);
        effects.Glow.Color.Resolve(null).ShouldBe(0xFFFFFF00u);
    }

    [Fact]
    public void Parse_Reflection_ReadsOpacities()
    {
        var refl = new XElement(
            A + "reflection",
            new XAttribute("blurRad", "6350"),
            new XAttribute("stA", "50000"),
            new XAttribute("endA", "300"),
            new XAttribute("dist", "5000"),
            new XAttribute("dir", "5400000")
        );
        var effects = new EffectFormat();
        EffectParser.Parse(WithEffects(refl), effects);

        effects.Reflection.ShouldNotBeNull();
        effects.Reflection.StartOpacityPercent.ShouldBe(50.0, 0.01);
        effects.Reflection.EndOpacityPercent.ShouldBe(0.3, 0.01);
    }

    [Fact]
    public void Parse_SoftEdge_ReadsRadius()
    {
        var soft = new XElement(A + "softEdge", new XAttribute("rad", "12700"));
        var effects = new EffectFormat();
        EffectParser.Parse(WithEffects(soft), effects);

        effects.SoftEdge.ShouldNotBeNull();
        effects.SoftEdge.Radius.Value.ShouldBe(12_700);
    }

    [Fact]
    public void Parse_Blur_ReadsRadiusAndGrow()
    {
        var blur = new XElement(
            A + "blur",
            new XAttribute("rad", "9000"),
            new XAttribute("grow", "0")
        );
        var effects = new EffectFormat();
        EffectParser.Parse(WithEffects(blur), effects);

        effects.Blur.ShouldNotBeNull();
        effects.Blur.Radius.Value.ShouldBe(9_000);
        effects.Blur.GrowBounds.ShouldBeFalse();
    }

    [Fact]
    public void Parse_Blur_DefaultGrowTrue()
    {
        var blur = new XElement(A + "blur", new XAttribute("rad", "1"));
        var effects = new EffectFormat();
        EffectParser.Parse(WithEffects(blur), effects);

        effects.Blur.ShouldNotBeNull();
        effects.Blur.GrowBounds.ShouldBeTrue();
    }

    [Fact]
    public void Parse_MultipleEffects_AllPopulated()
    {
        var effects = new EffectFormat();
        EffectParser.Parse(
            WithEffects(
                new XElement(A + "glow", new XAttribute("rad", "1")),
                new XElement(A + "blur", new XAttribute("rad", "1"))
            ),
            effects
        );

        effects.Glow.ShouldNotBeNull();
        effects.Blur.ShouldNotBeNull();
        effects.IsEmpty.ShouldBeFalse();
    }
}
