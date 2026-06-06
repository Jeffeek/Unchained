using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class MediaStoreTests : PptxTestBase
{
    private static byte[] FakePng(byte seed = 0) =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, seed];

    // ── AddImage ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddImage_StoresInImages()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Media.AddImage(FakePng(), "image/png");
        doc.Media.Images.Count.ShouldBe(1);
    }

    [Fact]
    public void AddImage_MultipleImages_AllStored()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Media.AddImage(FakePng(1), "image/png");
        doc.Media.AddImage(FakePng(2), "image/jpeg");
        doc.Media.Images.Count.ShouldBe(2);
    }

    // ── RemoveUnused ──────────────────────────────────────────────────────────

    [Fact]
    public void RemoveUnused_NoImagesNoShapes_ReturnsZero()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Media.RemoveUnused(doc.Slides).ShouldBe(0);
    }

    [Fact]
    public void RemoveUnused_UnreferencedImage_RemovesIt()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Media.AddImage(FakePng(), "image/png"); // not added to any shape

        var removed = doc.Media.RemoveUnused(doc.Slides);

        removed.ShouldBe(1);
        doc.Media.Images.Count.ShouldBe(0);
    }

    [Fact]
    public void RemoveUnused_ReferencedImage_PreservesIt()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = doc.Media.AddImage(FakePng(), "image/png");
        doc.Slides[0].Shapes.AddPicture(
            image,
            Emu.FromInches(1), Emu.FromInches(1),
            Emu.FromInches(3), Emu.FromInches(2));

        var removed = doc.Media.RemoveUnused(doc.Slides);

        removed.ShouldBe(0);
        doc.Media.Images.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveUnused_MixedImages_RemovesOnlyUnreferenced()
    {
        var doc = PptxFixtures.WithSlides(1);

        // Referenced image
        var referenced = doc.Media.AddImage(FakePng(1), "image/png");
        doc.Slides[0].Shapes.AddPicture(
            referenced,
            Emu.Zero, Emu.Zero,
            Emu.FromInches(3), Emu.FromInches(2));

        // Unreferenced image
        doc.Media.AddImage(FakePng(2), "image/png");

        var removed = doc.Media.RemoveUnused(doc.Slides);

        removed.ShouldBe(1);
        doc.Media.Images.Count.ShouldBe(1);
        doc.Media.Images[0].ShouldBe(referenced);
    }

    [Fact]
    public void RemoveUnused_MultipleUnreferenced_RemovesAll()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Media.AddImage(FakePng(1), "image/png");
        doc.Media.AddImage(FakePng(2), "image/png");
        doc.Media.AddImage(FakePng(3), "image/png");

        var removed = doc.Media.RemoveUnused(doc.Slides);

        removed.ShouldBe(3);
        doc.Media.Images.Count.ShouldBe(0);
    }

    [Fact]
    public void RemoveUnused_AfterSlideRemoval_PurgesOrphanedImage()
    {
        var doc = PptxFixtures.WithSlides(2);

        // Add image to slide 2 only
        var image = doc.Media.AddImage(FakePng(), "image/png");
        doc.Slides[1].Shapes.AddPicture(
            image,
            Emu.Zero, Emu.Zero,
            Emu.FromInches(3), Emu.FromInches(2));

        // Remove slide 2
        doc.Slides.Remove(doc.Slides[1]);

        // Image is now unreferenced
        var removed = doc.Media.RemoveUnused(doc.Slides);

        removed.ShouldBe(1);
        doc.Media.Images.Count.ShouldBe(0);
    }
}
