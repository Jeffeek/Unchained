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
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Name", "Alice"), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields().Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetFormFields_FieldName_Matches()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm(fieldName: "Email"), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields()[0].Name.ShouldBe("Email");
    }

    [Fact]
    public async Task GetFormFields_FieldValue_Matches()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Field", fieldValue: "Hello"), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields()[0].Value.ShouldBe("Hello");
    }

    [Fact]
    public async Task GetFormFields_FieldType_IsTx()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm(), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields()[0].FieldType.ShouldBe("Tx");
    }

    [Fact]
    public async Task GetFormFields_NoAcroForm_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFormFields_MultipleFields_ReturnsAll()
    {
        var fields = new List<(string, string)> { ("First", "1"), ("Second", "2"), ("Third", "3") };
        await using var doc = await LoadAsync(PdfFixtures.WithMultipleAcroFormFields(fields), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields().Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetFormFields_MultipleFields_NamesMatch()
    {
        var fields = new List<(string, string)> { ("Alpha", "a"), ("Beta", "b") };
        await using var doc = await LoadAsync(PdfFixtures.WithMultipleAcroFormFields(fields), ct: TestContext.Current.CancellationToken);
        var names = doc.GetFormFields().Select(static f => f.Name).ToList();
        names.ShouldContain("Alpha");
        names.ShouldContain("Beta");
    }

    [Fact]
    public async Task GetFormFields_HierarchicalForm_ReturnsQualifiedNames()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithHierarchicalAcroForm(), ct: TestContext.Current.CancellationToken);
        var names = doc.GetFormFields().Select(static f => f.Name).ToList();
        names.ShouldContain("Group.First");
        names.ShouldContain("Group.Second");
    }

    [Fact]
    public async Task GetFormFields_HierarchicalForm_CountIsChildrenOnly()
    {
        // The non-terminal group node should not appear as a field itself.
        await using var doc = await LoadAsync(PdfFixtures.WithHierarchicalAcroForm(), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields().Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetFormFields_BtnField_FieldTypeIsBtn()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithBtnAcroForm("Accept"), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields()[0].FieldType.ShouldBe("Btn");
    }

    [Fact]
    public async Task GetFormFields_BtnField_NameMatches()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithBtnAcroForm("Accept"), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields()[0].Name.ShouldBe("Accept");
    }

    // ── FillAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task FillAsync_UpdatesFieldValue()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Name", string.Empty), ct: TestContext.Current.CancellationToken);
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["Name"] = "Alice" }, ct: TestContext.Current.CancellationToken);
        doc.GetFormFields()[0].Value.ShouldBe("Alice");
    }

    [Fact]
    public async Task FillAsync_UnknownField_NoError()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Name", string.Empty), ct: TestContext.Current.CancellationToken);
        await Should.NotThrowAsync(() => Filler.FillAsync(doc, new Dictionary<string, string> { ["DoesNotExist"] = "X" }));
    }

    [Fact]
    public async Task FillAsync_EmptyValues_DocumentUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Name", "Original"), ct: TestContext.Current.CancellationToken);
        await Filler.FillAsync(doc, new Dictionary<string, string>(), ct: TestContext.Current.CancellationToken);
        doc.GetFormFields()[0].Value.ShouldBe("Original");
    }

    [Fact]
    public async Task FillAsync_RoundTrip_ValuePersistsAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Email", string.Empty), ct: TestContext.Current.CancellationToken);
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["Email"] = "test@example.com" }, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);
        reloaded.GetFormFields()[0].Value.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task FillAsync_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("F"), ct: TestContext.Current.CancellationToken);
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["F"] = "val" }, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task FillAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm(), ct: TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Filler.FillAsync(doc, new Dictionary<string, string>(), cts.Token));
    }

    [Fact]
    public async Task FillAsync_MultipleFields_AllValuesUpdated()
    {
        var fields = new List<(string, string)> { ("First", string.Empty), ("Second", string.Empty) };
        await using var doc = await LoadAsync(PdfFixtures.WithMultipleAcroFormFields(fields), ct: TestContext.Current.CancellationToken);
        await Filler.FillAsync(
            doc,
            new Dictionary<string, string> { ["First"] = "A", ["Second"] = "B" },
            ct: TestContext.Current.CancellationToken);
        var map = doc.GetFormFields().ToDictionary(static f => f.Name, static f => f.Value);
        map["First"].ShouldBe("A");
        map["Second"].ShouldBe("B");
    }

    [Fact]
    public async Task FillAsync_MultipleFields_PartialFill_OtherFieldsUnchanged()
    {
        var fields = new List<(string, string)> { ("First", "original"), ("Second", "keep") };
        await using var doc = await LoadAsync(PdfFixtures.WithMultipleAcroFormFields(fields), ct: TestContext.Current.CancellationToken);
        await Filler.FillAsync(
            doc,
            new Dictionary<string, string> { ["First"] = "changed" },
            ct: TestContext.Current.CancellationToken);
        var map = doc.GetFormFields().ToDictionary(static f => f.Name, static f => f.Value);
        map["First"].ShouldBe("changed");
        map["Second"].ShouldBe("keep");
    }

    [Fact]
    public async Task FillAsync_HierarchicalField_UpdatesByQualifiedName()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithHierarchicalAcroForm(), ct: TestContext.Current.CancellationToken);
        await Filler.FillAsync(
            doc,
            new Dictionary<string, string> { ["Group.First"] = "updated" },
            ct: TestContext.Current.CancellationToken);
        var map = doc.GetFormFields().ToDictionary(static f => f.Name, static f => f.Value);
        map["Group.First"].ShouldBe("updated");
        map["Group.Second"].ShouldBe("v2");
    }

    [Fact]
    public async Task FillAsync_HierarchicalField_RoundTrip_ValuePersists()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithHierarchicalAcroForm(), ct: TestContext.Current.CancellationToken);
        await Filler.FillAsync(
            doc,
            // ReSharper disable once StringLiteralTypo
            new Dictionary<string, string> { ["Group.Second"] = "newval" },
            ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);
        var map = reloaded.GetFormFields().ToDictionary(static f => f.Name, static f => f.Value);
        // ReSharper disable once StringLiteralTypo
        map["Group.Second"].ShouldBe("newval");
    }

    [Fact]
    public async Task FillAsync_WrongDocumentType_ThrowsArgumentException()
    {
        var badDoc = new FakeDocument();
        await Should.ThrowAsync<ArgumentException>(() =>
            Filler.FillAsync(badDoc, new Dictionary<string, string> { ["x"] = "y" }));
    }

    // ── FlattenAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task FlattenAsync_RemovesAcroFormFromDocument()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("F", "val"), ct: TestContext.Current.CancellationToken);
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.GetFormFields().ShouldBeEmpty();
    }

    [Fact]
    public async Task FlattenAsync_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("F"), ct: TestContext.Current.CancellationToken);
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task FlattenAsync_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("F", "v"), ct: TestContext.Current.CancellationToken);
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
        reloaded.GetFormFields().ShouldBeEmpty();
    }

    [Fact]
    public async Task FlattenAsync_WithAppearanceStream_MergesContentIntoPage()
    {
        // Field has /AP /N pointing to a stream object — FlattenAsync should append
        // that stream to the page /Contents and remove the AcroForm.
        await using var doc = await LoadAsync(PdfFixtures.WithAcroFormAndAppearance("F", "val"), ct: TestContext.Current.CancellationToken);
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.GetFormFields().ShouldBeEmpty();
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task FlattenAsync_WithAppearanceStream_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroFormAndAppearance("F", "val"), ct: TestContext.Current.CancellationToken);
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
        reloaded.GetFormFields().ShouldBeEmpty();
    }

    [Fact]
    public async Task FlattenAsync_BtnField_IsNotFlattenedAsTx_AcroFormStillRemoved()
    {
        // Btn fields do not have Tx appearance streams; FlattenAsync should still
        // remove the /AcroForm catalog entry even when no field is flattened.
        await using var doc = await LoadAsync(PdfFixtures.WithBtnAcroForm("Accept"), ct: TestContext.Current.CancellationToken);
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.GetFormFields().ShouldBeEmpty();
    }

    [Fact]
    public async Task FlattenAsync_MultipleFields_AllRemovedFromAcroForm()
    {
        var fields = new List<(string, string)> { ("F1", "a"), ("F2", "b") };
        await using var doc = await LoadAsync(PdfFixtures.WithMultipleAcroFormFields(fields), ct: TestContext.Current.CancellationToken);
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.GetFormFields().ShouldBeEmpty();
    }

    [Fact]
    public async Task FlattenAsync_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm(), ct: TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Filler.FlattenAsync(doc, cts.Token));
    }

    [Fact]
    public async Task FlattenAsync_WrongDocumentType_ThrowsArgumentException()
    {
        var badDoc = new FakeDocument();
        await Should.ThrowAsync<ArgumentException>(() => Filler.FlattenAsync(badDoc));
    }

    // ── FillAsync then FlattenAsync pipeline ──────────────────────────────────

    [Fact]
    public async Task Fill_ThenFlatten_ProducesNoFormFields()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroForm("Name", string.Empty), ct: TestContext.Current.CancellationToken);
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["Name"] = "Alice" }, ct: TestContext.Current.CancellationToken);
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.GetFormFields().ShouldBeEmpty();
    }

    [Fact]
    public async Task Fill_ThenFlatten_WithAppearance_RoundTrip_ParseableAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithAcroFormAndAppearance("F", string.Empty), ct: TestContext.Current.CancellationToken);
        await Filler.FillAsync(doc, new Dictionary<string, string> { ["F"] = "hello" }, ct: TestContext.Current.CancellationToken);
        await Filler.FlattenAsync(doc, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);
        reloaded.GetFormFields().ShouldBeEmpty();
    }

    // ── Minimal stub to trigger wrong-type error paths ────────────────────────

    private sealed class FakeDocument : Abstractions.IPdfDocument
    {
        public int PageCount => 0;
        public Abstractions.IPageCollection Pages => throw new NotSupportedException();
        public Models.DocumentMetadata Metadata => Models.DocumentMetadata.Empty;
        public bool IsEncrypted => false;
        public Models.PdfPermissions Permissions => Models.PdfPermissions.All;
        public Models.PdfEncryptionAlgorithm? CryptoAlgorithm => null;
        public bool IsDisposed => false;
        public bool IsLinearized => false;
        public bool IsTagged => false;
        public bool IsPdfaCompliant => false;
        public bool IsPdfUaCompliant => false;
        public (string First, string Second)? Id => null;
        public IReadOnlyList<Models.Bookmark> GetBookmarks() => [];
        public IReadOnlyList<Models.FormField> GetFormFields() => [];
        public Models.ViewerPreferences GetViewerPreferences() => Models.ViewerPreferences.Default;
        public Models.PageLayout PageLayout => Models.PageLayout.Default;
        public Models.PageMode PageMode => Models.PageMode.Default;
        public string? GetXmpMetadata() => null;
        public IReadOnlyList<Models.NamedDestination> GetNamedDestinations() => [];
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
