using Shouldly;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Shapes;

public sealed class VideoShapeTests
{
    [Fact]
    public void Defaults()
    {
        var video = new VideoShape();
        video.Video.ShouldBeNull();
        video.PosterFrame.ShouldBeNull();
        video.AutoPlay.ShouldBeFalse();
        video.Loop.ShouldBeFalse();
        video.HideWhenStopped.ShouldBeFalse();
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var clip = new EmbeddedVideo { ContentType = "video/mp4" };
        var poster = new EmbeddedImage("image/png", new byte[] { 1 });
        var video = new VideoShape
        {
            Video = clip,
            PosterFrame = poster,
            AutoPlay = true,
            Loop = true,
            HideWhenStopped = true
        };
        video.Video.ShouldBeSameAs(clip);
        video.PosterFrame.ShouldBeSameAs(poster);
        video.AutoPlay.ShouldBeTrue();
        video.Loop.ShouldBeTrue();
        video.HideWhenStopped.ShouldBeTrue();
    }
}
