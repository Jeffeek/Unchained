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
