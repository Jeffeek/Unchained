using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>M-C: WordArt (run effects + glyph outline + text warp) and shape 3-D round-trips.</summary>
public sealed class WordArtAndThreeDTests : PptxTestBase
{
    [Fact]
    public async Task RunEffectsAndOutline_RoundTrip()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddTextBox(
            Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(2), "Glow");
        var run = shape.TextFrame.Paragraphs[0].Runs[0];
        run.Format.Effects.Glow = new GlowEffect { Color = ColorSpec.FromRgb(0, 0xB0, 0xF0), Radius = Emu.FromPoints(5) };
        run.Format.Outline = new LineFormat();
        run.Format.Outline.SetSolid(ColorSpec.FromRgb(0, 0, 0), 1.5);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var r = reloaded.Slides[0].Shapes.OfType<AutoShape>()
            .SelectMany(static s => s.TextFrame.Paragraphs).SelectMany(static p => p.Runs)
            .First(static x => !x.Format.Effects.IsEmpty);
        r.Format.Effects.Glow.ShouldNotBeNull();
        r.Format.Effects.Glow.Radius.Value.ShouldBe(Emu.FromPoints(5).Value);
        r.Format.Outline.ShouldNotBeNull();
        r.Format.Outline.WidthPoints.ShouldBe(1.5);
    }

    [Fact]
    public async Task TextWarp_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddTextBox(
            Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(2), "Arched");
        shape.TextFrame.Format.Warp = new TextWarpFormat { Preset = "textArchUp" };

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var s = reloaded.Slides[0].Shapes.OfType<AutoShape>()
            .First(static x => x.TextFrame.Format.Warp is not null);
        s.TextFrame.Format.Warp!.Preset.ShouldBe("textArchUp");
    }

    [Fact]
    public async Task Shape3D_RoundTrips()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddShape(
            AutoShapeType.Rectangle, Emu.Zero, Emu.Zero, Emu.FromInches(2), Emu.FromInches(1));
        shape.ThreeD.ExtrusionHeight = Emu.FromPoints(10);
        shape.ThreeD.Material = "metal";
        shape.ThreeD.TopBevel = new BevelFormat { Width = Emu.FromPoints(6), Height = Emu.FromPoints(6), Preset = "circle" };

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        var s = reloaded.Slides[0].Shapes.OfType<AutoShape>().First(static x => !x.ThreeD.IsEmpty);
        s.ThreeD.ExtrusionHeight.Value.ShouldBe(Emu.FromPoints(10).Value);
        s.ThreeD.Material.ShouldBe("metal");
        s.ThreeD.TopBevel.ShouldNotBeNull();
        s.ThreeD.TopBevel.Width.Value.ShouldBe(Emu.FromPoints(6).Value);
    }
}
