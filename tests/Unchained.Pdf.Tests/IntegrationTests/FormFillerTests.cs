using Shouldly;
using Unchained.Pdf.Engine;
using Xunit;
using Unchained.Pdf.Tests.Helpers;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class FormFillerTests : PdfTestBase
{
    private static readonly FormFiller Filler = new();


    // ── GetFormFields (reading) ───────────────────────────────────────────────

    [Fact]
    public async Task GetFormFields_WithAcroForm_ReturnsField()
    {
        await using var doc = await LoadAsync(
            PdfFixtures.WithAcroForm("Name", "Alice"));
        doc.GetFormFields().Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetFormFields_FieldName_Matches()
    {
        await using var doc = await LoadAsync(
            PdfFixtures.WithAcroForm(fieldName: "Email"));
        doc.GetFormFields()[0].Name.ShouldBe("Email");
    }

    [Fact]
    public async Task GetFormFields_FieldValue_Matches()
    {
        await using var doc = await LoadAsync(
            PdfFixtures.WithAcroForm("Field", fieldValue: "Hello"));
        doc.GetFormFields()[0].Value.ShouldBe("Hello");
    }

    [Fact]
    public async Task GetFormFields_FieldType_IsTx()
    {
        await using var doc = await LoadAsync(
            PdfFixtures.WithAcroForm());
        doc.GetFormFields()[0].FieldType.ShouldBe("Tx");
    }

    [Fact]
    public async Task GetFormFields_NoAcroForm_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        doc.GetFormFields().ShouldBeEmpty();
    }

    // ── FillAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task FillAsync_UpdatesFieldValue()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Name", string.Empty));
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["Name"] = "Alice" }, ct: TestContext.Current.CancellationToken);
        doc.GetFormFields()[0].Value.ShouldBe("Alice");
    }

    [Fact]
    public async Task FillAsync_UnknownField_NoError()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Name", string.Empty));
        await Should.NotThrowAsync(() => Filler.FillAsync(doc, new Dictionary<string, string> { ["DoesNotExist"] = "X" }));
    }

    [Fact]
    public async Task FillAsync_EmptyValues_DocumentUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Name", "Original"));
        await Filler.FillAsync(doc, new Dictionary<string, string>(), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields()[0].Value.ShouldBe("Original");
    }

    [Fact]
    public async Task FillAsync_RoundTrip_ValuePersistsAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Email", string.Empty));
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["Email"] = "test@example.com" }, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms);
        reloaded.GetFormFields()[0].Value.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task FillAsync_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("F"));
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["F"] = "val" }, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task FillAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Filler.FillAsync(doc, new Dictionary<string, string>(), cts.Token));
    }

    // ── FlattenAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task FlattenAsync_RemovesAcroFormFromDocument()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("F", "val"));
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.GetFormFields().ShouldBeEmpty();
    }

    [Fact]
    public async Task FlattenAsync_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("F"));
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task FlattenAsync_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("F", "v"));
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms);
        reloaded.PageCount.ShouldBe(1);
        reloaded.GetFormFields().ShouldBeEmpty();
    }
}
