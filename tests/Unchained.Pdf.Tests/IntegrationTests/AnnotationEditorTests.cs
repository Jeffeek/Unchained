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
}
