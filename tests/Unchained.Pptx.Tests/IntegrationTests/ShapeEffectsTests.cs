using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>M-B: shape effects (shadow/glow/reflection/soft-edge/blur) model + round-trip.</summary>
public sealed class ShapeEffectsTests : PptxTestBase
{
    private static AutoShape NewShape()
    {
        var doc = PptxFixtures.WithSlides(1);
        return doc.Slides[0]
            .Shapes.AddShape(
                AutoShapeType.Rectangle,
                Emu.Zero,
                Emu.Zero,
                Emu.FromInches(2),
                Emu.FromInches(1)
            );
    }

    [Fact]
    public void Effects_DefaultEmpty() => NewShape().Effects.IsEmpty.ShouldBeTrue();

    [Fact]
    public async Task OuterShadow_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(
                AutoShapeType.Rectangle,
                Emu.Zero,
                Emu.Zero,
                Emu.FromInches(2),
                Emu.FromInches(1)
            );
        shape.Effects.OuterShadow = new OuterShadowEffect
        {
            Color = ColorSpec.FromRgb(0x33, 0x33, 0x33),
            BlurRadius = Emu.FromPoints(4),
            Distance = Emu.FromPoints(3),
            DirectionDegrees = 45
        };

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var s = reloaded.Slides[0].Shapes.OfType<AutoShape>().First(static x => !x.Effects.IsEmpty);
        s.Effects.OuterShadow.ShouldNotBeNull();
        s.Effects.OuterShadow.BlurRadius.Value.ShouldBe(Emu.FromPoints(4).Value);
        s.Effects.OuterShadow.Distance.Value.ShouldBe(Emu.FromPoints(3).Value);
        Math.Round(s.Effects.OuterShadow.DirectionDegrees).ShouldBe(45);
    }

    [Fact]
    public async Task GlowAndReflectionAndSoftEdge_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(
                AutoShapeType.Ellipse,
                Emu.Zero,
                Emu.Zero,
                Emu.FromInches(2),
                Emu.FromInches(1)
            );
        shape.Effects.Glow = new GlowEffect { Color = ColorSpec.FromRgb(0, 0xB0, 0xF0), Radius = Emu.FromPoints(6) };
        shape.Effects.Reflection = new ReflectionEffect { BlurRadius = Emu.FromPoints(2), StartOpacityPercent = 60, EndOpacityPercent = 0 };
        shape.Effects.SoftEdge = new SoftEdgeEffect { Radius = Emu.FromPoints(5) };

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var s = reloaded.Slides[0].Shapes.OfType<AutoShape>().First(static x => !x.Effects.IsEmpty);
        s.Effects.Glow.ShouldNotBeNull();
        s.Effects.Glow.Radius.Value.ShouldBe(Emu.FromPoints(6).Value);
        s.Effects.Reflection.ShouldNotBeNull();
        Math.Round(s.Effects.Reflection.StartOpacityPercent).ShouldBe(60);
        s.Effects.SoftEdge.ShouldNotBeNull();
        s.Effects.SoftEdge.Radius.Value.ShouldBe(Emu.FromPoints(5).Value);
    }

    [Fact]
    public async Task Blur_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0]
            .Shapes.AddShape(
                AutoShapeType.Rectangle,
                Emu.Zero,
                Emu.Zero,
                Emu.FromInches(2),
                Emu.FromInches(1)
            );
        shape.Effects.Blur = new BlurEffect { Radius = Emu.FromPoints(3), GrowBounds = true };

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var s = reloaded.Slides[0].Shapes.OfType<AutoShape>().First(static x => !x.Effects.IsEmpty);
        s.Effects.Blur.ShouldNotBeNull();
        s.Effects.Blur.Radius.Value.ShouldBe(Emu.FromPoints(3).Value);
    }
}
