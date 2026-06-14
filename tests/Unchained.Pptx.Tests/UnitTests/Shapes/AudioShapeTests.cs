using Shouldly;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Shapes;

public sealed class AudioShapeTests
{
    [Fact]
    public void Defaults()
    {
        var audio = new AudioShape();
        audio.Audio.ShouldBeNull();
        audio.AutoPlay.ShouldBeFalse();
        audio.HideIcon.ShouldBeFalse();
        audio.Loop.ShouldBeFalse();
        audio.PlayAcrossSlides.ShouldBeFalse();
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var clip = new EmbeddedAudio { ContentType = "audio/mpeg" };
        var audio = new AudioShape
        {
            Audio = clip,
            AutoPlay = true,
            HideIcon = true,
            Loop = true,
            PlayAcrossSlides = true
        };
        audio.Audio.ShouldBeSameAs(clip);
        audio.AutoPlay.ShouldBeTrue();
        audio.HideIcon.ShouldBeTrue();
        audio.Loop.ShouldBeTrue();
        audio.PlayAcrossSlides.ShouldBeTrue();
    }

    [Fact]
    public void IsShape() => new AudioShape().ShouldBeAssignableTo<Shape>();
}
