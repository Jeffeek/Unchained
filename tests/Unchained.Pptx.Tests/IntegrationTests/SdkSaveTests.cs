using System.IO.Packaging;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Animations;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     M5b: the SDK-backed save re-emits modelled slides while preserving every other part. Compared
///     against the custom writer (which rebuilds the package and drops parts), the SDK save must keep
///     the part set intact and still reload to the same slide structure.
/// </summary>
public sealed class SdkSaveTests
{
    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", name);

    private static int CountParts(byte[] pptx)
    {
        using var ms = new MemoryStream(pptx);
        using var pkg = Package.Open(ms, FileMode.Open, FileAccess.Read);
        return pkg.GetParts().Count();
    }

    private static IEnumerable<string> SlideXmls(byte[] pptx)
    {
        using var ms = new MemoryStream(pptx);
        using var pkg = Package.Open(ms, FileMode.Open, FileAccess.Read);
        var result = new List<string>();
        foreach (var part in from part in pkg.GetParts()
                             let uri = part.Uri.OriginalString
                             where uri.Contains("/ppt/slides/slide", StringComparison.Ordinal) && uri.EndsWith(".xml", StringComparison.Ordinal)
                             select part)
        {
            using var stream = part.GetStream();
            using var reader = new StreamReader(stream);
            result.Add(reader.ReadToEnd());
        }

        return result;
    }

    [
        Theory,
        InlineData("shp-shapes.pptx"),
        InlineData("cht-charts.pptx"),
        InlineData("prs-notes.pptx"),
        InlineData("tbl-cell.pptx"),
        InlineData("mst-slide-layouts.pptx")
    ]
    public async Task SdkSave_PreservesAllParts(string fileName)
    {
        var path = SamplePath(fileName);
        File.Exists(path).ShouldBeTrue($"sample {fileName} missing");
        var original = await File.ReadAllBytesAsync(path);
        var beforeParts = CountParts(original);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });
        var saved = ms.ToArray();

        // The SDK save must not drop parts (the custom writer does).
        CountParts(saved).ShouldBe(beforeParts, $"{fileName}: SDK save must preserve all parts");

        await doc.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_ThenReload_PreservesSlideStructure()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });
        var originalShapeCounts = doc.Slides.Select(static s => s.Shapes.Count).ToArray();

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        reloaded.Slides.Count.ShouldBe(doc.Slides.Count, "slide count after SDK save round-trip");
        reloaded.Slides.Select(static s => s.Shapes.Count)
            .ToArray()
            .ShouldBe(originalShapeCounts, "per-slide shape count after SDK save round-trip");

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_MovedPicture_PersistsPositionAndKeepsImage()
    {
        // Regression: editing a shape's geometry then SDK-saving must keep the picture's image and
        // all parts (the writer used to regenerate the whole slide, breaking blip relationships).
        var path = SamplePath("shp-picture.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);
        var beforeParts = CountParts(original);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var picture = doc.Slides.SelectMany(static s => s.Shapes).OfType<PictureShape>().First();
        picture.Image.ShouldNotBeNull();
        var movedX = picture.X + Emu.FromInches(1);
        picture.X = movedX;

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });
        var saved = ms.ToArray();

        CountParts(saved).ShouldBe(beforeParts, "media and other parts must survive a geometry edit");

        var reloaded = await processor.LoadAsync(saved, new OpenOptions { UseOpenXmlEngine = true });
        var movedPicture = reloaded.Slides.SelectMany(static s => s.Shapes).OfType<PictureShape>().First();
        movedPicture.X.Value.ShouldBe(movedX.Value, "moved picture position must persist through SDK save");
        movedPicture.Image.ShouldNotBeNull("picture image must survive the edit — this was the corruption");

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_EditedText_PersistsAndKeepsParts()
    {
        // Regression: changing a paragraph's text then SDK-saving must persist the new text and
        // keep all parts (the writer used to regenerate the slide and disturb text/relationships).
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);
        var beforeParts = CountParts(original);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var paragraph = doc.Slides.SelectMany(static s => s.Shapes)
            .OfType<AutoShape>()
            .SelectMany(static s => s.TextFrame.Paragraphs)
            .FirstOrDefault();
        paragraph.ShouldNotBeNull("sample has no editable paragraph");

        const string edited = "Unchained edit marker 12345";
        paragraph.PlainText = edited;

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });
        var saved = ms.ToArray();

        CountParts(saved).ShouldBe(beforeParts, "parts must survive a text edit");

        var reloaded = await processor.LoadAsync(saved, new OpenOptions { UseOpenXmlEngine = true });
        reloaded.Slides.SelectMany(static s => s.Shapes)
            .OfType<AutoShape>()
            .SelectMany(static s => s.TextFrame.Paragraphs)
            .Select(static p => p.PlainText)
            .ShouldContain(edited, "edited paragraph text must persist through SDK save");

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_EditedNameAltAndHidden_Persist()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var shape = doc.Slides.SelectMany(static s => s.Shapes).First();
        shape.Name = "Renamed marker";
        shape.AltText = "Alt marker";
        shape.IsHidden = true;

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        var reloadedShape = reloaded.Slides.SelectMany(static s => s.Shapes).First(static s => s.Name == "Renamed marker");
        reloadedShape.AltText.ShouldBe("Alt marker");
        reloadedShape.IsHidden.ShouldBeTrue();

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_AddedShadow_Persists()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var shape = doc.Slides.SelectMany(static s => s.Shapes).OfType<AutoShape>().First();
        shape.Name = "Shadowed marker";
        shape.Effects.OuterShadow = new OuterShadowEffect();

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        var reloadedShape = reloaded.Slides.SelectMany(static s => s.Shapes).First(static s => s.Name == "Shadowed marker");
        reloadedShape.Effects.OuterShadow.ShouldNotBeNull("added shadow must persist through SDK save");

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_DeletedShape_IsRemoved()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var slideIndex = 0;
        while (slideIndex < doc.Slides.Count && doc.Slides[slideIndex].Shapes.Count == 0)
            slideIndex++;
        slideIndex.ShouldBeLessThan(doc.Slides.Count, "sample must have a slide with shapes");

        var slide = doc.Slides[slideIndex];
        var before = slide.Shapes.Count;
        slide.Shapes.Remove(slide.Shapes[0]);

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        reloaded.Slides[slideIndex].Shapes.Count.ShouldBe(before - 1, "deleted shape must not reappear");

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_AddedTextBox_Persists()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var slide = doc.Slides[0];
        var before = slide.Shapes.Count;
        slide.Shapes.AddTextBox(
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(4),
            Emu.FromInches(1),
            "Added box marker 999"
        );

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        reloaded.Slides[0].Shapes.Count.ShouldBe(before + 1, "added shape must persist");
        reloaded.Slides[0]
            .Shapes
            .OfType<AutoShape>()
            .SelectMany(static s => s.TextFrame.Paragraphs)
            .Select(static p => p.PlainText)
            .ShouldContain("Added box marker 999");

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_Hyperlink_Persists()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var shape = doc.Slides.SelectMany(static s => s.Shapes).First();
        shape.Name = "Linked marker";
        shape.ClickAction = HyperlinkAction.ToUrl("https://unchained.example/xyz");

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        var reloadedShape = reloaded.Slides.SelectMany(static s => s.Shapes).First(static s => s.Name == "Linked marker");
        reloadedShape.ClickAction.ShouldNotBeNull();
        reloadedShape.ClickAction.Url.ShouldBe("https://unchained.example/xyz");

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_Decorative_Persists()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var shape = doc.Slides.SelectMany(static s => s.Shapes).First();
        shape.Name = "Decorative marker";
        shape.IsDecorative = true;

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        reloaded.Slides.SelectMany(static s => s.Shapes)
            .First(static s => s.Name == "Decorative marker")
            .IsDecorative.ShouldBeTrue();

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_AddedPicture_KeepsImage()
    {
        var path = SamplePath("shp-picture.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var source = doc.Slides.SelectMany(static s => s.Shapes).OfType<PictureShape>().First();
        source.Image.ShouldNotBeNull();

        var slide = doc.Slides[0];
        var beforePictures = slide.Shapes.OfType<PictureShape>().Count();
        slide.Shapes.AddPicture(
            new EmbeddedImage(source.Image.ContentType, source.Image.Data.ToArray()),
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(2),
            Emu.FromInches(2)
        );

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        var reloadedPictures = reloaded.Slides[0].Shapes.OfType<PictureShape>().ToList();
        reloadedPictures.Count.ShouldBe(beforePictures + 1, "added picture must persist");
        reloadedPictures.ShouldAllBe(static p => p.Image != null, "every picture (incl. the added one) must keep its image");

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_SolidFillOnInheritingShape_Persists()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        // A shape with no explicit fill inherits from the theme — the new writer must be able to
        // apply a fill to it (previously the edit was silently dropped).
        var shape = doc.Slides.SelectMany(static s => s.Shapes)
            .OfType<AutoShape>()
            .FirstOrDefault(static s => s.Fill.Type == FillType.None);
        shape.ShouldNotBeNull("sample has no inheriting-fill shape");

        shape.Name = "Filled marker";
        shape.Fill.SetSolid(ColorSpec.FromRgb(0x11, 0x99, 0x44));

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        var reloadedShape = reloaded.Slides.SelectMany(static s => s.Shapes).First(static s => s.Name == "Filled marker");
        reloadedShape.Fill.Type.ShouldBe(FillType.Solid, "applied solid fill must persist");
        reloadedShape.Fill.Solid.ShouldNotBeNull();
        (reloadedShape.Fill.Solid.Color.Resolve(null) & 0xFFFFFF).ShouldBe(0x119944u);

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_OutlineOnInheritingShape_Persists()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var shape = doc.Slides.SelectMany(static s => s.Shapes)
            .OfType<AutoShape>()
            .FirstOrDefault(static s => s.Line.Fill.Type == FillType.None && s.Line.WidthPoints is null);
        shape.ShouldNotBeNull("sample has no inheriting-line shape");

        shape.Name = "Outlined marker";
        shape.Line.SetSolid(ColorSpec.FromRgb(0xCC, 0x00, 0x00), 2.5);

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        var reloadedShape = reloaded.Slides.SelectMany(static s => s.Shapes).First(static s => s.Name == "Outlined marker");
        reloadedShape.Line.Fill.Type.ShouldBe(FillType.Solid, "applied outline must persist");
        reloadedShape.Line.WidthPoints.ShouldNotBeNull();

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_Transition_IsWritten()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });
        doc.Slides[0].Transition.Effect = TransitionEffect.Fade;

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        SlideXmls(ms.ToArray()).ShouldContain(static x => x.Contains(":transition", StringComparison.Ordinal), "a transition element must be written");
        await doc.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_Background_IsWritten()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });
        doc.Slides[0].Background.Fill.SetSolid(ColorSpec.FromRgb(0x12, 0x34, 0x56));

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        SlideXmls(ms.ToArray())
            .ShouldContain(
                static x => x.Contains(":bg>", StringComparison.Ordinal) && x.Contains("123456", StringComparison.OrdinalIgnoreCase),
                "a background with the chosen colour must be written"
            );
        await doc.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_SlideReorder_Persists()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });
        doc.Slides.Count.ShouldBeGreaterThanOrEqualTo(2, "sample needs at least two slides");

        var secondId = doc.Slides[1].SlideId;
        doc.Slides.MoveTo(1, 0); // move the second slide to the front

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        reloaded.Slides[0].SlideId.ShouldBe(secondId, "reordered slide order must persist");

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_SlideDelete_Persists()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });
        doc.Slides.Count.ShouldBeGreaterThanOrEqualTo(2, "sample needs at least two slides");

        var before = doc.Slides.Count;
        var keptId = doc.Slides[0].SlideId;
        doc.Slides.RemoveAt(1);

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        reloaded.Slides.Count.ShouldBe(before - 1, "deleted slide must not reappear");
        reloaded.Slides[0].SlideId.ShouldBe(keptId);

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_SlideAdd_Persists()
    {
        var path = SamplePath("shp-shapes.pptx");
        File.Exists(path).ShouldBeTrue("sample missing");
        var original = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(original, new OpenOptions { UseOpenXmlEngine = true });

        var before = doc.Slides.Count;
        var added = doc.Slides.AddBlank(doc.Masters[0].Layouts[0]);
        added.Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(1), "New slide marker");

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true });

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        reloaded.Slides.Count.ShouldBe(before + 1, "added slide must persist");
        reloaded.Slides.SelectMany(static s => s.Shapes)
            .OfType<AutoShape>()
            .SelectMany(static s => s.TextFrame.Paragraphs)
            .Select(static p => p.PlainText)
            .ShouldContain("New slide marker");

        await doc.DisposeAsync();
        await reloaded.DisposeAsync();
    }

    [Fact]
    public async Task SdkSave_FallsBackToCustomWriter_WhenNoEngine()
    {
        // A CreateBlank document has no engine; requesting the SDK save must still succeed
        // (falls back to the custom writer) rather than throw.
        using var processor = new PresentationProcessor();
        var doc = processor.CreateBlank();
        doc.Slides.AddBlank(doc.Masters[0].Layouts[0]);

        using var ms = new MemoryStream();
        await Should.NotThrowAsync(() =>
            processor.SaveAsync(doc, ms, new SaveOptions { UseOpenXmlEngine = true })
        );
        ms.Length.ShouldBeGreaterThan(0);

        await doc.DisposeAsync();
    }
}
