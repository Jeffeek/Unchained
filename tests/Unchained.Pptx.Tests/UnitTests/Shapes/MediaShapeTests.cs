using Shouldly;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Shapes;

public sealed class MediaShapeTests
{
    [Fact]
    public void AudioShape_Defaults()
    {
        var audio = new AudioShape();
        audio.Audio.ShouldBeNull();
        audio.AutoPlay.ShouldBeFalse();
        audio.HideIcon.ShouldBeFalse();
        audio.Loop.ShouldBeFalse();
        audio.PlayAcrossSlides.ShouldBeFalse();
    }

    [Fact]
    public void AudioShape_Properties_RoundTrip()
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
    public void AudioShape_IsShape() => new AudioShape().ShouldBeAssignableTo<Shape>();

    [Fact]
    public void VideoShape_Defaults()
    {
        var video = new VideoShape();
        video.Video.ShouldBeNull();
        video.PosterFrame.ShouldBeNull();
        video.AutoPlay.ShouldBeFalse();
        video.Loop.ShouldBeFalse();
        video.HideWhenStopped.ShouldBeFalse();
    }

    [Fact]
    public void VideoShape_Properties_RoundTrip()
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

    [Fact]
    public void OleShape_Defaults()
    {
        var ole = new OleShape();
        ole.EmbeddedData.Length.ShouldBe(0);
        ole.ProgId.ShouldBe(string.Empty);
        ole.LinkedFilePath.ShouldBeNull();
    }

    [Fact]
    public void OleShape_Properties_RoundTrip()
    {
        var ole = new OleShape
        {
            EmbeddedData = new byte[] { 1, 2, 3 },
            ProgId = "Excel.Sheet.12",
            LinkedFilePath = @"C:\book.xlsx"
        };
        ole.EmbeddedData.Length.ShouldBe(3);
        ole.ProgId.ShouldBe("Excel.Sheet.12");
        ole.LinkedFilePath.ShouldBe(@"C:\book.xlsx");
    }
}

public sealed class ConnectorShapeTests
{
    [Fact]
    public void Defaults_StraightNoConnections()
    {
        var connector = new ConnectorShape();
        connector.ConnectorType.ShouldBe(ConnectorType.Straight);
        connector.StartConnection.ShouldBeNull();
        connector.EndConnection.ShouldBeNull();
    }

    [Fact]
    public void Connections_RoundTrip()
    {
        var connector = new ConnectorShape
        {
            ConnectorType = ConnectorType.Bent,
            StartConnection = new ConnectionEndpoint(5, 0),
            EndConnection = new ConnectionEndpoint(9, 2)
        };
        connector.ConnectorType.ShouldBe(ConnectorType.Bent);
        connector.StartConnection.ShouldBe(new ConnectionEndpoint(5, 0));
        connector.EndConnection!.TargetShapeId.ShouldBe(9u);
        connector.EndConnection.ConnectionPointIndex.ShouldBe(2);
    }
}

public sealed class ConnectionEndpointTests
{
    [Fact]
    public void Constructor_StoresFields()
    {
        var endpoint = new ConnectionEndpoint(42, 3);
        endpoint.TargetShapeId.ShouldBe(42u);
        endpoint.ConnectionPointIndex.ShouldBe(3);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() =>
        new ConnectionEndpoint(1, 2).ShouldBe(new ConnectionEndpoint(1, 2));

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual() =>
        new ConnectionEndpoint(1, 2).ShouldNotBe(new ConnectionEndpoint(1, 3));
}

public sealed class SmartArtNodeTests
{
    [Fact]
    public void Defaults_EmptyTextNoChildren()
    {
        var node = new SmartArtNode();
        node.ModelId.ShouldBe(string.Empty);
        node.Text.ShouldBe(string.Empty);
        node.Children.ShouldBeEmpty();
    }

    [Fact]
    public void AddChild_AppendsAndReturnsChild()
    {
        var root = new SmartArtNode { Text = "Root" };
        var child = root.AddChild("Child");
        child.Text.ShouldBe("Child");
        root.Children.Count.ShouldBe(1);
        root.Children[0].ShouldBeSameAs(child);
    }

    [Fact]
    public void AddChild_Nested_BuildsHierarchy()
    {
        var root = new SmartArtNode();
        var child = root.AddChild("A");
        var grandchild = child.AddChild("B");
        root.Children[0].Children[0].ShouldBeSameAs(grandchild);
        grandchild.Text.ShouldBe("B");
    }
}
