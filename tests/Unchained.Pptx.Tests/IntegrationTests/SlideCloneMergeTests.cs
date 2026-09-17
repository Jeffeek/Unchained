using Shouldly;
using System.IO.Compression;
using Unchained.Drawing.Constants;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class SlideCloneMergeTests : PptxTestBase
{
    private static byte[] FakePng(byte seed = 0) => [.. PngConstants.Signature, seed];

    private static void AddPicture(Slide slide, EmbeddedImage image) =>
        slide.Shapes.AddPicture(
            image,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2)
        );

    // ── Same-deck clone (regression: XML round-trip used to drop the image) ────

    [Fact]
    public void AddClone_SameDeck_PreservesImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = doc.Media.AddImage(FakePng(), "image/png");
        AddPicture(doc.Slides[0], image);

        var clone = doc.Slides.AddClone(doc.Slides[0]);

        var picture = clone.Shapes.OfType<PictureShape>().Single();
        picture.Image.ShouldNotBeNull();
        picture.Image.Data.ToArray().ShouldBe(FakePng());
    }

    [Fact]
    public void AddClone_SameDeck_DoesNotDuplicateImage()
    {
        var doc = PptxFixtures.WithSlides(1);
        var image = doc.Media.AddImage(FakePng(), "image/png");
        AddPicture(doc.Slides[0], image);

        doc.Slides.AddClone(doc.Slides[0]);

        doc.Media.Images.Count.ShouldBe(1);
    }

    [Fact]
    public void AddClone_SameDeck_KeepsSameLayoutInstance()
    {
        var doc = PptxFixtures.WithSlides(1);
        var clone = doc.Slides.AddClone(doc.Slides[0]);
        clone.Layout.ShouldBe(doc.Slides[0].Layout);
    }

    // ── Cross-deck clone ──────────────────────────────────────────────────────

    [Fact]
    public void AddClone_CrossDeck_ImportsImageIntoTargetStore()
    {
        var source = PptxFixtures.WithSlides(1);
        var image = source.Media.AddImage(FakePng(7), "image/png");
        AddPicture(source.Slides[0], image);
        var target = PptxFixtures.BlankPresentation();

        var clone = target.Slides.AddClone(source.Slides[0]);

        target.Media.Images.Count.ShouldBe(1);
        var picture = clone.Shapes.OfType<PictureShape>().Single();
        picture.Image.ShouldNotBeNull();
        picture.Image.Data.ToArray().ShouldBe(FakePng(7));
        target.Media.Images.ShouldContain(picture.Image);
    }

    [Fact]
    public void AddClone_CrossDeck_ImportsMasterIntoTarget()
    {
        var source = PptxFixtures.WithSlides(1);
        var target = PptxFixtures.BlankPresentation();
        var mastersBefore = target.Masters.Count;

        var clone = target.Slides.AddClone(source.Slides[0]);

        target.Masters.Count.ShouldBe(mastersBefore + 1);
        target.Masters.ShouldContain(clone.Layout.Master);
    }

    [Fact]
    public void AddClone_CrossDeck_TwoSlidesShareOneImportedMaster()
    {
        var source = PptxFixtures.WithSlides(2);
        var target = PptxFixtures.BlankPresentation();
        var mastersBefore = target.Masters.Count;

        target.Slides.AddClone(source.Slides[0]);
        target.Slides.AddClone(source.Slides[1]);

        target.Masters.Count.ShouldBe(mastersBefore + 1);
    }

    [Fact]
    public void AddClone_CrossDeck_DeduplicatesIdenticalImages()
    {
        var source = PptxFixtures.WithSlides(2);
        var image = source.Media.AddImage(FakePng(3), "image/png");
        AddPicture(source.Slides[0], image);
        AddPicture(source.Slides[1], image);
        var target = PptxFixtures.BlankPresentation();

        target.MergeSlidesFrom(source);

        target.Media.Images.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AddClone_CrossDeck_ImageSurvivesRoundTrip()
    {
        var source = PptxFixtures.WithSlides(1);
        var image = source.Media.AddImage(FakePng(9), "image/png");
        AddPicture(source.Slides[0], image);
        var target = PptxFixtures.BlankPresentation();
        target.Slides.AddClone(source.Slides[0]);

        var reloaded = await PptxFixtures.RoundTripAsync(target);

        reloaded.Slides.Count.ShouldBe(1);
        var picture = reloaded.Slides[0].Shapes.OfType<PictureShape>().Single();
        picture.Image.ShouldNotBeNull();
        picture.Image.Data.ToArray().ShouldBe(FakePng(9));
    }

    // ── MergeSlidesFrom ───────────────────────────────────────────────────────

    [Fact]
    public void MergeSlidesFrom_AppendsAllSourceSlides()
    {
        var source = PptxFixtures.WithSlides(3);
        var target = PptxFixtures.WithSlides(1);

        var added = target.MergeSlidesFrom(source);

        added.Count.ShouldBe(3);
        target.Slides.Count.ShouldBe(4);
    }

    [Fact]
    public void MergeSlidesFrom_DoesNotModifySource()
    {
        var source = PptxFixtures.WithSlides(2);
        var target = PptxFixtures.BlankPresentation();

        target.MergeSlidesFrom(source);

        source.Slides.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MergeSlidesFrom_MergedDeck_RoundTrips()
    {
        var source = PptxFixtures.WithSlides(2);
        var target = PptxFixtures.WithSlides(1);
        target.MergeSlidesFrom(source);

        var reloaded = await PptxFixtures.RoundTripAsync(target);

        reloaded.Slides.Count.ShouldBe(3);
    }

    // ── Background fill ───────────────────────────────────────────────────────

    [Fact]
    public void AddClone_CopiesSlideBackgroundFill()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Background.Fill.SetSolid(ColorSpec.FromRgb(10, 20, 30));

        var clone = doc.Slides.AddClone(doc.Slides[0]);

        clone.Background.Fill.Type.ShouldBe(FillType.Solid);
        clone.Background.Fill.Solid!.Color.ShouldBe(ColorSpec.FromRgb(10, 20, 30));
    }

    [Fact]
    public void AddClone_CrossDeck_ImportsMasterBackgroundFill()
    {
        var source = PptxFixtures.WithSlides(1);
        source.Masters[0].Background.Fill.SetSolid(ColorSpec.FromRgb(40, 60, 80));
        var target = PptxFixtures.BlankPresentation();

        var clone = target.Slides.AddClone(source.Slides[0]);

        clone.Layout.Master.Background.Fill.Type.ShouldBe(FillType.Solid);
        clone.Layout.Master.Background.Fill.Solid!.Color.ShouldBe(ColorSpec.FromRgb(40, 60, 80));
    }

    // ── Charts ────────────────────────────────────────────────────────────────

    [Fact]
    public void AddClone_CrossDeck_TransfersChart()
    {
        var source = PptxFixtures.WithSlides(1);
        source.Slides[0]
            .Shapes.AddChart(
                ChartType.BarClustered,
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(4),
                Emu.FromInches(3)
            );
        var target = PptxFixtures.BlankPresentation();

        var clone = target.Slides.AddClone(source.Slides[0]);

        var chart = clone.Shapes.OfType<ChartShape>().Single();
        chart.Chart.Type.ShouldBe(ChartType.BarClustered);
    }

    [Fact]
    public async Task AddClone_CrossDeck_ChartSurvivesRoundTrip()
    {
        var source = PptxFixtures.WithSlides(1);
        source.Slides[0]
            .Shapes.AddChart(
                ChartType.Pie,
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(4),
                Emu.FromInches(3)
            );
        var target = PptxFixtures.BlankPresentation();
        target.Slides.AddClone(source.Slides[0]);

        var reloaded = await PptxFixtures.RoundTripAsync(target);

        reloaded.Slides[0].Shapes.OfType<ChartShape>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task AddClone_TwoChartsOnClonedSlide_GetDistinctParts()
    {
        // Regression: cloned charts must receive fresh relationship IDs so they do not collide.
        var source = PptxFixtures.WithSlides(1);
        source.Slides[0].Shapes.AddChart(ChartType.Pie, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(2));
        source.Slides[0].Shapes.AddChart(ChartType.BarClustered, Emu.FromInches(5), Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(2));
        var target = PptxFixtures.BlankPresentation();
        target.Slides.AddClone(source.Slides[0]);

        var reloaded = await PptxFixtures.RoundTripAsync(target);

        reloaded.Slides[0].Shapes.OfType<ChartShape>().Count().ShouldBe(2);
    }

    // ── SmartArt (from a real fixture with a diagram) ─────────────────────────

    private static Task<byte[]> SmartArtSampleAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", "shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("SmartArt sample missing");
        return File.ReadAllBytesAsync(path);
    }

    private static bool PartExists(byte[] pptx, string partName)
    {
        using var ms = new MemoryStream(pptx);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        return archive.GetEntry(partName) != null;
    }

    [Fact]
    public async Task AddClone_CrossDeck_TransfersSmartArt()
    {
        var source = await Processor.LoadAsync(await SmartArtSampleAsync(), cancellationToken: TestContext.Current.CancellationToken);
        var sourceSlide = source.Slides.First(static s => s.Shapes.OfType<SmartArtShape>().Any());
        var originalText = sourceSlide.Shapes.OfType<SmartArtShape>().First().GetAllText();
        var target = PptxFixtures.BlankPresentation();

        var clone = target.Slides.AddClone(sourceSlide);

        var smartArt = clone.Shapes.OfType<SmartArtShape>().Single();
        smartArt.GetAllText().ShouldBe(originalText);
        smartArt.DataPartData.ShouldNotBeNull();
    }

    [Fact]
    public async Task AddClone_CrossDeck_SmartArtPartsWrittenOnSave()
    {
        var source = await Processor.LoadAsync(await SmartArtSampleAsync(), cancellationToken: TestContext.Current.CancellationToken);
        var sourceSlide = source.Slides.First(static s => s.Shapes.OfType<SmartArtShape>().Any());
        var target = PptxFixtures.BlankPresentation();
        target.Slides.AddClone(sourceSlide);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(target, ms, cancellationToken: TestContext.Current.CancellationToken);
        var saved = ms.ToArray();

        PartExists(saved, "ppt/diagrams/importedData1.xml").ShouldBeTrue();
        PartExists(saved, "ppt/diagrams/importedLayout1.xml").ShouldBeTrue();
    }

    [Fact]
    public async Task AddClone_CrossDeck_SmartArtNodeTextSurvivesRoundTrip()
    {
        var source = await Processor.LoadAsync(await SmartArtSampleAsync(), cancellationToken: TestContext.Current.CancellationToken);
        var sourceSlide = source.Slides.First(static s => s.Shapes.OfType<SmartArtShape>().Any());
        var originalText = sourceSlide.Shapes.OfType<SmartArtShape>().First().GetAllText();
        var target = PptxFixtures.BlankPresentation();
        target.Slides.AddClone(sourceSlide);

        var reloaded = await PptxFixtures.RoundTripAsync(target);

        var smartArt = reloaded.Slides.SelectMany(static s => s.Shapes).OfType<SmartArtShape>().Single();
        smartArt.GetAllText().ShouldBe(originalText);
    }
}
