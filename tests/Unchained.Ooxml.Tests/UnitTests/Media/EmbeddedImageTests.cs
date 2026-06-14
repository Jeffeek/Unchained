using Shouldly;
using Unchained.Ooxml.Media;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Media;

public sealed class EmbeddedImageTests
{
    [Fact]
    public void Constructor_SetsContentTypeAndData()
    {
        var image = new EmbeddedImage("image/png", new byte[] { 1, 2, 3, 4 });
        image.ContentType.ShouldBe("image/png");
        image.Data.Length.ShouldBe(4);
        image.PixelWidth.ShouldBe(0);
        image.PixelHeight.ShouldBe(0);
    }

    [Fact]
    public void Constructor_NullContentType_Throws() =>
        Should.Throw<ArgumentNullException>(static () => new EmbeddedImage(null!, new byte[] { 1 }));

    [Fact]
    public void PixelDimensions_RoundTrip()
    {
        var image = new EmbeddedImage("image/jpeg", new byte[] { 0 })
        {
            PixelWidth = 640,
            PixelHeight = 480
        };
        image.PixelWidth.ShouldBe(640);
        image.PixelHeight.ShouldBe(480);
    }
}
