using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Xunit;
using Unchained.Pdf.Tests.Helpers;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class AnnotationEditorTests : PdfTestBase
{
    private static readonly AnnotationEditor Editor = new();


    private static readonly Annotation SampleAnnotation = new(
        Subtype: AnnotationSubtype.Text,
        X: 100,
        Y: 700,
        Width: 50,
        Height: 50,
        Contents: "Hello"
    );

    // ── GetAnnotations (reading existing) ────────────────────────────────────

    [Fact]
    public async Task GetAnnotations_PageWithAnnotation_ReturnsOne()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAnnotation(contents: "Note"));
        doc.Pages[1].GetAnnotations().Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAnnotations_PageWithAnnotation_ContentsMatch()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAnnotation(contents: "MyNote"));
        var annots = doc.Pages[1].GetAnnotations();
        annots[0].Contents.ShouldBe("MyNote");
    }

    [Fact]
    public async Task GetAnnotations_PageWithAnnotation_SubtypeIsText()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAnnotation());
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(AnnotationSubtype.Text);
    }

    [Fact]
    public async Task GetAnnotations_EmptyPage_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        doc.Pages[1].GetAnnotations().ShouldBeEmpty();
    }

    // ── AddAnnotationAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task AddAnnotationAsync_EmptyPage_AnnotationAdded()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        await Editor.AddAnnotationAsync(doc, pageNumber: 1, SampleAnnotation, ct: TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations().Count.ShouldBe(1);
    }

    [Fact]
    public async Task AddAnnotationAsync_Contents_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, ct: TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Contents.ShouldBe("Hello");
    }

    [Fact]
    public async Task AddAnnotationAsync_Subtype_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        await Editor.AddAnnotationAsync(doc, 1, new Annotation(AnnotationSubtype.Square, 10, 10, 50, 50), ct: TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(AnnotationSubtype.Square);
    }

    [Fact]
    public async Task AddAnnotationAsync_Rect_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        var ann = new Annotation(AnnotationSubtype.Text, X: 30, Y: 40, Width: 60, Height: 70);
        await Editor.AddAnnotationAsync(doc, 1, ann, ct: TestContext.Current.CancellationToken);
        var result = doc.Pages[1].GetAnnotations()[0];
        result.X.ShouldBe(30, tolerance: 0.01f);
        result.Y.ShouldBe(40, tolerance: 0.01f);
        result.Width.ShouldBe(60, tolerance: 0.01f);
        result.Height.ShouldBe(70, tolerance: 0.01f);
    }

    [Fact]
    public async Task AddAnnotationAsync_MultipleAnnotations_AllPresent()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, ct: TestContext.Current.CancellationToken);
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation with { Contents = "Second" }, ct: TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations().Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddAnnotationAsync_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(count: 2));
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task AddAnnotationAsync_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        await Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms);
        reloaded.Pages[1].GetAnnotations().Count.ShouldBe(1);
    }

    [Fact]
    public async Task AddAnnotationAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Editor.AddAnnotationAsync(doc, 1, SampleAnnotation, cts.Token));
    }
}
