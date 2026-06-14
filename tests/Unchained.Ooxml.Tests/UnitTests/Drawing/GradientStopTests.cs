using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Drawing;

public sealed class GradientStopTests
{
    [Fact]
    public void Constructor_SetsPositionAndColor()
    {
        var color = ColorSpec.FromRgb(0x44, 0x72, 0xC4);
        var stop = new GradientStop(0.5, color);
        stop.Position.ShouldBe(0.5);
        stop.Color.ShouldBe(color);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var color = ColorSpec.FromRgb(1, 2, 3);
        new GradientStop(0.25, color).ShouldBe(new GradientStop(0.25, color));
    }

    [Fact]
    public void RecordEquality_DifferentPosition_AreNotEqual()
    {
        var color = ColorSpec.FromRgb(1, 2, 3);
        new GradientStop(0.0, color).ShouldNotBe(new GradientStop(1.0, color));
    }
}

public sealed class PictureFillTests
{
    [Fact]
    public void Defaults_FillMode_NoImage()
    {
        var fill = new PictureFill();
        fill.StretchMode.ShouldBe(PictureStretchMode.Fill);
        fill.Image.ShouldBeNull();
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var image = new EmbeddedImage("image/png", new byte[] { 1, 2, 3 });
        var fill = new PictureFill
        {
            Image = image,
            StretchMode = PictureStretchMode.Tile
        };
        fill.Image.ShouldBeSameAs(image);
        fill.StretchMode.ShouldBe(PictureStretchMode.Tile);
    }
}
