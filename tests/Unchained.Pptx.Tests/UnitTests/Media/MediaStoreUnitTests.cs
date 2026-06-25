using Shouldly;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Media;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Media;

/// <summary>
///     Unit tests for <see cref="MediaStore" /> — add/list images, audio, video, and fonts, plus the
///     <see cref="MediaStore.FindFontData" /> style-matching fallback chain.
/// </summary>
public sealed class MediaStoreTests
{
    private static MediaStore Store() => new();

    [Fact]
    public void AddImage_FromBytes_StoresImage()
    {
        var store = Store();
        var image = store.AddImage(new byte[] { 1, 2, 3 }, "image/png");
        image.ContentType.ShouldBe("image/png");
        store.Images.Count.ShouldBe(1);
    }

    [Fact]
    public void AddImage_NullContentType_Throws() =>
        Should.Throw<ArgumentNullException>(static () => Store().AddImage(new byte[] { 1 }, null!));

    [Fact]
    public void AddImage_Instance_Stores()
    {
        var store = Store();
        var image = new EmbeddedImage("image/jpeg", new byte[] { 9 });
        store.AddImage(image);
        store.Images.ShouldContain(image);
    }

    [Fact]
    public void AddAudio_Stores()
    {
        var store = Store();
        store.AddAudio(new EmbeddedAudio { ContentType = "audio/mpeg" });
        store.AudioFiles.Count.ShouldBe(1);
    }

    [Fact]
    public void AddVideo_Stores()
    {
        var store = Store();
        store.AddVideo(new EmbeddedVideo { ContentType = "video/mp4" });
        store.VideoFiles.Count.ShouldBe(1);
    }

    [Fact]
    public void AddFont_Stores()
    {
        var store = Store();
        store.AddFont(new EmbeddedFont { Typeface = "Arial", Data = new byte[] { 1 } });
        store.Fonts.Count.ShouldBe(1);
    }

    [Fact]
    public void FindFontData_NoFonts_ReturnsNull() =>
        Store().FindFontData("Arial", EmbeddedFontStyle.Regular).ShouldBeNull();

    [Fact]
    public void FindFontData_EmptyTypeface_ReturnsNull() =>
        Store().FindFontData(string.Empty, EmbeddedFontStyle.Regular).ShouldBeNull();

    [Fact]
    public void FindFontData_ExactStyleMatch_ReturnsIt()
    {
        var store = Store();
        store.AddFont(new EmbeddedFont { Typeface = "Arial", Style = EmbeddedFontStyle.Regular, Data = new byte[] { 1 } });
        store.AddFont(new EmbeddedFont { Typeface = "Arial", Style = EmbeddedFontStyle.Bold, Data = new byte[] { 2 } });

        var data = store.FindFontData("Arial", EmbeddedFontStyle.Bold);
        data.ShouldNotBeNull();
        data.Value.Span[0].ShouldBe((byte)2);
    }

    [Fact]
    public void FindFontData_NoExactStyle_FallsBackToRegular()
    {
        var store = Store();
        store.AddFont(new EmbeddedFont { Typeface = "Arial", Style = EmbeddedFontStyle.Regular, Data = new byte[] { 1 } });

        var data = store.FindFontData("Arial", EmbeddedFontStyle.BoldItalic);
        data.ShouldNotBeNull();
        data.Value.Span[0].ShouldBe((byte)1);
    }

    [Fact]
    public void FindFontData_NoRegular_FallsBackToAnyOfTypeface()
    {
        var store = Store();
        store.AddFont(new EmbeddedFont { Typeface = "Arial", Style = EmbeddedFontStyle.Italic, Data = new byte[] { 7 } });

        var data = store.FindFontData("Arial", EmbeddedFontStyle.Bold);
        data.ShouldNotBeNull();
        data.Value.Span[0].ShouldBe((byte)7);
    }

    [Fact]
    public void FindFontData_DifferentTypeface_ReturnsNull()
    {
        var store = Store();
        store.AddFont(new EmbeddedFont { Typeface = "Arial", Data = new byte[] { 1 } });
        store.FindFontData("Calibri", EmbeddedFontStyle.Regular).ShouldBeNull();
    }

    [Fact]
    public void FindFontData_TypefaceMatch_IsCaseInsensitive()
    {
        var store = Store();
        store.AddFont(new EmbeddedFont { Typeface = "Arial", Data = new byte[] { 1 } });
        store.FindFontData("ARIAL", EmbeddedFontStyle.Regular).ShouldNotBeNull();
    }

    // ── RemoveUnused ──────────────────────────────────────────────────────────

    [Fact]
    public void RemoveUnused_DropsUnreferencedImagesAudioAndVideo()
    {
        var doc = PptxFixtures.WithSlides(1);
        var store = doc.Media;
        var slide = doc.Slides[0];

        // Referenced image inside a group shape (exercises CollectImages group recursion).
        var usedImage = store.AddImage(new EmbeddedImage("image/png", new byte[] { 1 }));
        var group = slide.Shapes.AddGroup();
        group.Children.AddParsed(new PictureShape { Image = usedImage });

        // Referenced audio + video directly on the slide (CollectMedia branches).
        var usedAudio = store.AddAudio(new EmbeddedAudio { ContentType = "audio/mpeg" });
        var usedVideo = store.AddVideo(new EmbeddedVideo { ContentType = "video/mp4" });
        slide.Shapes.AddParsed(new AudioShape { Audio = usedAudio });
        slide.Shapes.AddParsed(new VideoShape { Video = usedVideo });

        // Unreferenced media that must be purged.
        store.AddImage(new EmbeddedImage("image/png", new byte[] { 2 }));
        store.AddAudio(new EmbeddedAudio { ContentType = "audio/wav" });
        store.AddVideo(new EmbeddedVideo { ContentType = "video/avi" });

        var removed = store.RemoveUnused(doc.Slides);

        removed.ShouldBe(3);
        store.Images.ShouldHaveSingleItem().ShouldBeSameAs(usedImage);
        store.AudioFiles.ShouldHaveSingleItem().ShouldBeSameAs(usedAudio);
        store.VideoFiles.ShouldHaveSingleItem().ShouldBeSameAs(usedVideo);
    }

    [Fact]
    public void RemoveUnused_NullSlides_Throws() =>
        Should.Throw<ArgumentNullException>(static () => Store().RemoveUnused(null!));
}
