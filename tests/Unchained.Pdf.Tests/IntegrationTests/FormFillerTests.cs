using Shouldly;
using Unchained.Pdf.Engine;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class FormFillerTests
{
    private static readonly FormFiller Filler = new();
    private static readonly DocumentProcessor Processor = new();

    private static Task<Abstractions.IPdfDocument> LoadAsync(byte[] bytes) =>
        Processor.LoadAsync(new MemoryStream(bytes));

    // ── GetFormFields (reading) ───────────────────────────────────────────────

    [Fact]
    public async Task GetFormFields_WithAcroForm_ReturnsField()
    {
        await using var doc = await LoadAsync(
            Helpers.PdfFixtures.WithAcroForm("Name", "Alice"));
        doc.GetFormFields().Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetFormFields_FieldName_Matches()
    {
        await using var doc = await LoadAsync(
            Helpers.PdfFixtures.WithAcroForm(fieldName: "Email"));
        doc.GetFormFields()[0].Name.ShouldBe("Email");
    }

    [Fact]
    public async Task GetFormFields_FieldValue_Matches()
    {
        await using var doc = await LoadAsync(
            Helpers.PdfFixtures.WithAcroForm("Field", fieldValue: "Hello"));
        doc.GetFormFields()[0].Value.ShouldBe("Hello");
    }

    [Fact]
    public async Task GetFormFields_FieldType_IsTx()
    {
        await using var doc = await LoadAsync(
            Helpers.PdfFixtures.WithAcroForm());
        doc.GetFormFields()[0].FieldType.ShouldBe("Tx");
    }

    [Fact]
    public async Task GetFormFields_NoAcroForm_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        doc.GetFormFields().ShouldBeEmpty();
    }

    // ── FillAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task FillAsync_UpdatesFieldValue()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithAcroForm("Name", string.Empty));
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["Name"] = "Alice" });
        doc.GetFormFields()[0].Value.ShouldBe("Alice");
    }

    [Fact]
    public async Task FillAsync_UnknownField_NoError()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithAcroForm("Name", string.Empty));
        await Should.NotThrowAsync(() => Filler.FillAsync(doc, new Dictionary<string, string> { ["DoesNotExist"] = "X" }));
    }

    [Fact]
    public async Task FillAsync_EmptyValues_DocumentUnchanged()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithAcroForm("Name", "Original"));
        await Filler.FillAsync(doc, new Dictionary<string, string>());
        doc.GetFormFields()[0].Value.ShouldBe("Original");
    }

    [Fact]
    public async Task FillAsync_RoundTrip_ValuePersistsAfterSave()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithAcroForm("Email", string.Empty));
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["Email"] = "test@example.com" });
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        await using var reloaded = await Processor.LoadAsync(ms);
        reloaded.GetFormFields()[0].Value.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task FillAsync_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithAcroForm("F"));
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["F"] = "val" });
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task FillAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithAcroForm());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Filler.FillAsync(doc, new Dictionary<string, string>(), cts.Token));
    }

    // ── FlattenAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task FlattenAsync_RemovesAcroFormFromDocument()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithAcroForm("F", "val"));
        await Filler.FlattenAsync(doc);
        doc.GetFormFields().ShouldBeEmpty();
    }

    [Fact]
    public async Task FlattenAsync_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithAcroForm("F"));
        await Filler.FlattenAsync(doc);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task FlattenAsync_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.WithAcroForm("F", "v"));
        await Filler.FlattenAsync(doc);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        await using var reloaded = await Processor.LoadAsync(ms);
        reloaded.PageCount.ShouldBe(1);
        reloaded.GetFormFields().ShouldBeEmpty();
    }
}
