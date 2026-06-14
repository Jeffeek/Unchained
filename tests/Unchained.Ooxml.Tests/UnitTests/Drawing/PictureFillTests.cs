using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Drawing;

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
