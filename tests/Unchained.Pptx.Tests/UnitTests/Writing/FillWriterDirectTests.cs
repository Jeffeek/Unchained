using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

/// <summary>
///     Direct branch coverage for <see cref="FillWriter" />: the gradient writer's linear and
///     non-linear (radial) paths, every pattern preset token (incl. the fallthrough default), and
///     the picture writer with and without an embedded image / relationship id.
/// </summary>
public sealed class FillWriterDirectTests
{
    private static XElement Write(FillFormat fill)
    {
        var parent = new XElement(DmlNames.Dml + "spPr");
        FillWriter.Write(parent, fill);
        return parent;
    }

    // ── Gradient: linear vs radial ────────────────────────────────────────────────

    [Fact]
    public void Write_LinearGradient_EmitsLinAttribute()
    {
        var fill = new FillFormat { Type = FillType.Gradient };
        var grad = new GradientFill { IsLinear = true, LinearAngleDegrees = 45 };
        grad.Stops.Add(new GradientStop(0.0, ColorSpec.FromRgb(0, 0, 0)));
        grad.Stops.Add(new GradientStop(1.0, ColorSpec.FromRgb(255, 255, 255)));
        fill.Gradient = grad;

        var parent = Write(fill);
        var gradEl = parent.Element(DmlNames.GradientFill);
        gradEl.ShouldNotBeNull();
        gradEl.Element(DmlNames.LinearGradient).ShouldNotBeNull();
        gradEl.Element(DmlNames.GradientStopList)!.Elements(DmlNames.GradientStop).Count().ShouldBe(2);
    }

    [Fact]
    public void Write_RadialGradient_OmitsLinElement()
    {
        var fill = new FillFormat { Type = FillType.Gradient };
        var grad = new GradientFill { IsLinear = false };
        grad.Stops.Add(new GradientStop(0.0, ColorSpec.FromRgb(10, 20, 30)));
        fill.Gradient = grad;

        var parent = Write(fill);
        var gradEl = parent.Element(DmlNames.GradientFill);
        gradEl.ShouldNotBeNull();
        gradEl.Element(DmlNames.LinearGradient).ShouldBeNull();
        gradEl.Element(DmlNames.GradientStopList)!.Elements(DmlNames.GradientStop).Count().ShouldBe(1);
    }

    [Fact]
    public void Write_Gradient_NoStops_StillEmitsContainer()
    {
        var fill = new FillFormat { Type = FillType.Gradient, Gradient = new GradientFill { IsLinear = true } };
        var parent = Write(fill);
        parent.Element(DmlNames.GradientFill)!.Element(DmlNames.GradientStopList)!.Elements().ShouldBeEmpty();
    }

    // ── Pattern presets ─────────────────────────────────────────────────────────

    [
        Theory,
        InlineData(PatternPreset.Percent5, "pct5"),
        InlineData(PatternPreset.Percent10, "pct10"),
        InlineData(PatternPreset.Percent20, "pct20"),
        InlineData(PatternPreset.Percent25, "pct25"),
        InlineData(PatternPreset.Percent30, "pct30"),
        InlineData(PatternPreset.Percent40, "pct40"),
        InlineData(PatternPreset.Percent50, "pct50"),
        InlineData(PatternPreset.Percent60, "pct60"),
        InlineData(PatternPreset.Percent70, "pct70"),
        InlineData(PatternPreset.Percent75, "pct75"),
        InlineData(PatternPreset.Percent80, "pct80"),
        InlineData(PatternPreset.Percent90, "pct90"),
        InlineData(PatternPreset.HorizontalLines, "horz"),
        InlineData(PatternPreset.VerticalLines, "vert"),
        InlineData(PatternPreset.Trellis, "pct5")
    ]
    public void Write_Pattern_EmitsPrstToken(PatternPreset preset, string expectedToken)
    {
        var fill = new FillFormat
        {
            Type = FillType.Pattern,
            Pattern = new PatternFill
            {
                Preset = preset,
                ForegroundColor = ColorSpec.FromRgb(0, 0, 0),
                BackgroundColor = ColorSpec.FromRgb(255, 255, 255)
            }
        };

        var patt = Write(fill).Element(DmlNames.PatternFill);
        patt.ShouldNotBeNull();
        patt.Attribute("prst")!.Value.ShouldBe(expectedToken);
        patt.Element(DmlNames.Dml + "fgClr").ShouldNotBeNull();
        patt.Element(DmlNames.Dml + "bgClr").ShouldNotBeNull();
    }

    // ── Picture ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Write_Picture_WithImageAndRelId_EmitsBlipEmbed()
    {
        var image = new EmbeddedImage("image/png", new byte[] { 1, 2, 3 }) { RelationshipId = "rId42" };
        var fill = new FillFormat { Type = FillType.Picture, Picture = new PictureFill { Image = image } };

        var blipFill = Write(fill).Element(DmlNames.BlipFill);
        blipFill.ShouldNotBeNull();
        var blip = blipFill.Element(DmlNames.Blip);
        blip.ShouldNotBeNull();
        blip.Attribute(PmlNames.RelationshipId).ShouldNotBeNull();
        blipFill.Element(DmlNames.Stretch).ShouldNotBeNull();
    }

    [Fact]
    public void Write_Picture_ImageWithoutRelId_OmitsEmbedAttribute()
    {
        var image = new EmbeddedImage("image/png", new byte[] { 1 });
        var fill = new FillFormat { Type = FillType.Picture, Picture = new PictureFill { Image = image } };

        var blip = Write(fill).Element(DmlNames.BlipFill)!.Element(DmlNames.Blip);
        blip.ShouldNotBeNull();
        blip.Attribute(PmlNames.RelationshipId).ShouldBeNull();
    }

    [Fact]
    public void Write_Picture_NoImage_EmitsBlipFillWithoutBlip()
    {
        var fill = new FillFormat { Type = FillType.Picture, Picture = new PictureFill() };
        var blipFill = Write(fill).Element(DmlNames.BlipFill);
        blipFill.ShouldNotBeNull();
        blipFill.Element(DmlNames.Blip).ShouldBeNull();
        blipFill.Element(DmlNames.Stretch).ShouldNotBeNull();
    }

    // ── Default / guarded branches ─────────────────────────────────────────────────

    [Fact]
    public void Write_GradientTypeButNullGradient_ThrowsOutOfRange()
    {
        var fill = new FillFormat { Type = FillType.Gradient, Gradient = null };
        Should.Throw<ArgumentOutOfRangeException>(() => Write(fill));
    }

    [Fact]
    public void Write_PatternTypeButNullPattern_ThrowsOutOfRange()
    {
        var fill = new FillFormat { Type = FillType.Pattern, Pattern = null };
        Should.Throw<ArgumentOutOfRangeException>(() => Write(fill));
    }

    [Fact]
    public void Write_PictureTypeButNullPicture_ThrowsOutOfRange()
    {
        var fill = new FillFormat { Type = FillType.Picture, Picture = null };
        Should.Throw<ArgumentOutOfRangeException>(() => Write(fill));
    }

    [Fact]
    public void Write_SolidTypeButNullSolid_ThrowsOutOfRange()
    {
        var fill = new FillFormat { Type = FillType.Solid, Solid = null };
        Should.Throw<ArgumentOutOfRangeException>(() => Write(fill));
    }
}
