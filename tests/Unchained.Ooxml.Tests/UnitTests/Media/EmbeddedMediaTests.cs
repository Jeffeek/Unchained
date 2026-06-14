using Shouldly;
using Unchained.Ooxml.Media;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Media;

public sealed class EmbeddedAudioTests
{
    [Fact]
    public void Embedded_HasData_IsEmbeddedTrue()
    {
        var audio = new EmbeddedAudio
        {
            Data = new byte[] { 1, 2, 3 },
            ContentType = "audio/mpeg"
        };
        audio.IsEmbedded.ShouldBeTrue();
        audio.ContentType.ShouldBe("audio/mpeg");
        audio.Data!.Value.Length.ShouldBe(3);
    }

    [Fact]
    public void Linked_NoData_IsEmbeddedFalse()
    {
        var audio = new EmbeddedAudio { LinkedFilePath = @"C:\clip.wav" };
        audio.IsEmbedded.ShouldBeFalse();
        audio.LinkedFilePath.ShouldBe(@"C:\clip.wav");
        audio.ContentType.ShouldBe(string.Empty);
    }
}

public sealed class EmbeddedVideoTests
{
    [Fact]
    public void Embedded_HasData_IsEmbeddedTrue()
    {
        var video = new EmbeddedVideo
        {
            Data = new byte[] { 9, 8, 7, 6 },
            ContentType = "video/mp4"
        };
        video.IsEmbedded.ShouldBeTrue();
        video.ContentType.ShouldBe("video/mp4");
        video.Data!.Value.Length.ShouldBe(4);
    }

    [Fact]
    public void Linked_NoData_IsEmbeddedFalse()
    {
        var video = new EmbeddedVideo { LinkedFilePath = @"C:\movie.mp4" };
        video.IsEmbedded.ShouldBeFalse();
        video.LinkedFilePath.ShouldBe(@"C:\movie.mp4");
    }
}

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
        Should.Throw<System.ArgumentNullException>(() => new EmbeddedImage(null!, new byte[] { 1 }));

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
