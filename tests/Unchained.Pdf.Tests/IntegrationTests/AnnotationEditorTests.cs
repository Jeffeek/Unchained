using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class AnnotationEditorTests : PdfTestBase
{
    private static readonly AnnotationEditor Editor = new();


    private static readonly Annotation SampleAnnotation = new(
        AnnotationSubtype.Text,
        100,
        700,
        50,
        50,
        "Hello"
    );

    // ── GetAnnotations (reading existing) ────────────────────────────────────

    [Fact]
    public async Task GetAnnotations_PageWithAnnotation_ReturnsOne()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAnnotation(), TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations().Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAnnotations_PageWithAnnotation_ContentsMatch()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAnnotation("MyNote"), TestContext.Current.CancellationToken);
        var annots = doc.Pages[1].GetAnnotations();
        annots[0].Contents.ShouldBe("MyNote");
    }

    [Fact]
    public async Task GetAnnotations_PageWithAnnotation_SubtypeIsText()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAnnotation(), TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(AnnotationSubtype.Text);
    }

    [Fact]
    public async Task GetAnnotations_EmptyPage_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations().ShouldBeEmpty();
    }

    // ── AddAnnotationAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task AddAnnotationAsync_EmptyPage_AnnotationAdded()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations().Count.ShouldBe(1);
    }

    [Fact]
    public async Task AddAnnotationAsync_Contents_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Contents.ShouldBe("Hello");
    }

    [Fact]
    public async Task AddAnnotationAsync_Subtype_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.AddAnnotationAsync(doc, 1, new Annotation(AnnotationSubtype.Square, 10, 10, 50, 50), TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(AnnotationSubtype.Square);
    }

    [Fact]
    public async Task AddAnnotationAsync_Rect_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(AnnotationSubtype.Text, 30, 40, 60, 70);
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        var result = doc.Pages[1].GetAnnotations()[0];
        result.X.ShouldBe(30, 0.01f);
        result.Y.ShouldBe(40, 0.01f);
        result.Width.ShouldBe(60, 0.01f);
        result.Height.ShouldBe(70, 0.01f);
    }

    [Fact]
    public async Task AddAnnotationAsync_MultipleAnnotations_AllPresent()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, TestContext.Current.CancellationToken);
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation with { Contents = "Second" }, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations().Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddAnnotationAsync_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task AddAnnotationAsync_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.Pages[1].GetAnnotations().Count.ShouldBe(1);
    }

    [Fact]
    public async Task AddAnnotationAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, cts.Token));
    }

    [Fact]
    public async Task AddAnnotation_WithColor_ColorRoundTripped()
    {
        // Exercises the `if (annotation.Color is { Length: 3 } c)` branch.
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(
            AnnotationSubtype.Square,
            10,
            10,
            100,
            50,
            Color: [1f, 0f, 0f]
        );
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        var result = doc.Pages[1].GetAnnotations()[0];
        result.Color.ShouldNotBeNull();
        result.Color!.Length.ShouldBe(3);
    }

    [Fact]
    public async Task AddAnnotation_HighlightSubtype_RoundTripped()
    {
        // Exercises the Highlight annotation subtype path.
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(AnnotationSubtype.Highlight, 50, 600, 200, 20);
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(AnnotationSubtype.Highlight);
    }

    [Fact]
    public async Task AddAnnotation_LinkSubtype_RoundTripped()
    {
        // Exercises the Link annotation subtype path.
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(AnnotationSubtype.Link, 50, 700, 150, 20);
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(AnnotationSubtype.Link);
    }

    [Fact]
    public async Task AddAnnotation_CircleSubtype_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(AnnotationSubtype.Circle, 200, 400, 80, 80);
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(AnnotationSubtype.Circle);
    }

    [Fact]
    public async Task AddAnnotation_FreeTextSubtype_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(
            AnnotationSubtype.FreeText,
            100,
            300,
            200,
            60,
            "Free text note"
        );
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        var result = doc.Pages[1].GetAnnotations()[0];
        result.Subtype.ShouldBe(AnnotationSubtype.FreeText);
        result.Contents.ShouldBe("Free text note");
    }

    [Fact]
    public async Task AddAnnotation_ToPageWithExistingAnnotations_AppendsCorrectly()
    {
        // Exercises ResolveAnnotArray with a real PdfArray already on the page dict
        // (the fixture embeds an annotation so /Annots is a plain array, not indirect).
        await using var doc = await LoadAsync(PdfFixtures.WithAnnotation("Existing"), TestContext.Current.CancellationToken);
        var ann = new Annotation(
            AnnotationSubtype.Text,
            200,
            600,
            50,
            50,
            "Added"
        );
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        var annots = doc.Pages[1].GetAnnotations();
        annots.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddAnnotation_ThreeOnSamePage_AllPresent()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        for (var i = 0; i < 3; i++)
        {
            var ann = new Annotation(
                AnnotationSubtype.Text,
                i * 60f,
                700,
                50,
                50,
                $"Note {i}"
            );
            await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        }

        doc.Pages[1].GetAnnotations().Count.ShouldBe(3);
    }

    [Fact]
    public async Task AddAnnotation_MultiPage_OnlyTargetPageAffected()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        var ann = new Annotation(AnnotationSubtype.Text, 100, 700, 50, 50);
        await Editor.AddAnnotationAsync(doc, 2, ann, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations().ShouldBeEmpty();
        doc.Pages[2].GetAnnotations().Count.ShouldBe(1);
        doc.Pages[3].GetAnnotations().ShouldBeEmpty();
    }

    [Fact]
    public async Task AddAnnotation_WithColorAndContents_BothPresent()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(
            AnnotationSubtype.Circle,
            10,
            10,
            80,
            80,
            "Colored circle",
            [0f, 0f, 1f]
        );
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        var result = doc.Pages[1].GetAnnotations()[0];
        result.Contents.ShouldBe("Colored circle");
        result.Color.ShouldNotBeNull();
    }

    [Fact]
    public async Task AddAnnotation_RoundTrip_ColorPreservedAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(
            AnnotationSubtype.Square,
            10,
            10,
            50,
            50,
            Color: [0f, 1f, 0f]
        );
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.Pages[1].GetAnnotations()[0].Color.ShouldNotBeNull();
    }
}
