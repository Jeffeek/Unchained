using Shouldly;
using Unchained.Ooxml.Media;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Media;

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
