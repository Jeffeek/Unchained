using System.Text;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Tests for document-level properties exposed on <see cref="Abstractions.IPdfDocument"/>:
/// <c>IsLinearized</c>, <c>IsTagged</c>, <c>IsPdfaCompliant</c>, <c>IsPdfUaCompliant</c>,
/// <c>CryptoAlgorithm</c>, <c>Id</c>, <c>IsXrefGapsAllowed</c>.
/// Also covers: <c>GetObjectByIdAsync</c>, <c>TrimCacheAsync</c>, <c>SetOpenActionAsync</c>,
/// <c>RemovePdfaComplianceAsync</c>, <c>RemovePdfUaComplianceAsync</c>,
/// <c>EmbedStandardFontsAsync</c>, <c>OptimizeSize</c> / <c>AllowReusePageContent</c>
/// <c>SaveOptions</c> flags, and the <c>ignoreCorruptedObjects</c> processor option.
/// </summary>
public sealed class DocumentPropertiesTests : PdfTestBase
{
    // ── IsLinearized ──────────────────────────────────────────────────────────

    [Fact]
    public async Task IsLinearized_NonLinearized_ReturnsFalse()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.IsLinearized.ShouldBeFalse();
    }

    [Fact]
    public async Task IsLinearized_AfterLinearizedSave_ReturnsTrue()
    {
        await using var source = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(source, ms, SaveOptions.WebOptimized, TestContext.Current.CancellationToken);

        await using var doc = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        doc.IsLinearized.ShouldBeTrue();
    }

    // ── IsTagged ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsTagged_UntaggedPdf_ReturnsFalse()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.IsTagged.ShouldBeFalse();
    }

    [Fact]
    public async Task IsTagged_TaggedPdf_ReturnsTrue()
    {
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello",
            new TxtLoadOptions(Tagged: true, Language: "en"),
            TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        reloaded.IsTagged.ShouldBeTrue();
    }

    // ── IsPdfaCompliant ───────────────────────────────────────────────────────

    [Fact]
    public async Task IsPdfaCompliant_PlainPdf_ReturnsFalse()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.IsPdfaCompliant.ShouldBeFalse();
    }

    [Fact]
    public async Task IsPdfaCompliant_AfterPdfAConversion_ReturnsTrue()
    {
        await using var source = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(source, ms, ct: TestContext.Current.CancellationToken);

        await using var doc = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        doc.IsPdfaCompliant.ShouldBeTrue();
    }

    // ── IsPdfUaCompliant ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsPdfUaCompliant_PlainPdf_ReturnsFalse()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.IsPdfUaCompliant.ShouldBeFalse();
    }

    // ── CryptoAlgorithm ───────────────────────────────────────────────────────

    [Fact]
    public async Task CryptoAlgorithm_UnencryptedDocument_ReturnsNull()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.CryptoAlgorithm.ShouldBeNull();
    }

    [Fact]
    public async Task CryptoAlgorithm_Aes256Encrypted_ReturnsAes256()
    {
        await using var source = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(source, ms, new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "pw")), TestContext.Current.CancellationToken);

        await using var doc = await Processor.LoadAsync(new MemoryStream(ms.ToArray()), "pw", TestContext.Current.CancellationToken);
        doc.CryptoAlgorithm.ShouldBe(PdfEncryptionAlgorithm.Aes256);
    }

    // ── Id ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Id_DocumentWithoutId_DoesNotThrow()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        _ = doc.Id; // must not throw
    }

    [Fact]
    public async Task Id_PdfADocument_ReturnsTwoHexStrings()
    {
        await using var source = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.ConvertToPdfAAsync(source, ms, ct: TestContext.Current.CancellationToken);

        await using var doc = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        var id = doc.Id;
        id.ShouldNotBeNull();
        id.Value.First.ShouldNotBeNullOrEmpty();
        id.Value.Second.ShouldNotBeNullOrEmpty();
    }

    // ── GetObjectById ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetObjectById_FirstObject_ReturnsNonNull()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var obj = await Processor.GetObjectByIdAsync(doc, 1, TestContext.Current.CancellationToken);
        obj.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetObjectById_NonExistentObjectNumber_ReturnsNull()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var obj = await Processor.GetObjectByIdAsync(doc, 999999, TestContext.Current.CancellationToken);
        obj.ShouldBeNull();
    }

    // ── TrimCache ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task TrimCache_DocumentRemainsAccessibleAfterTrim()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        await Processor.TrimCacheAsync(doc, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(3);
    }

    // ── SetOpenAction ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetOpenAction_ValidPage_WritesOpenActionToOutput()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        await Processor.SetOpenActionAsync(doc, 2, TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        Encoding.Latin1.GetString(ms.ToArray()).ShouldContain("/OpenAction");
    }

    [Fact]
    public async Task SetOpenAction_PageNumberOutOfRange_Throws()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => Processor.SetOpenActionAsync(doc, 99, TestContext.Current.CancellationToken));
    }

    // ── RemovePdfaCompliance ──────────────────────────────────────────────────

    [Fact]
    public async Task RemovePdfaCompliance_AfterConversion_IsPdfaCompliantReturnsFalse()
    {
        await using var source = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var converted = new MemoryStream();
        await Processor.ConvertToPdfAAsync(source, converted, ct: TestContext.Current.CancellationToken);

        await using var doc = await LoadAsync(converted.ToArray(), TestContext.Current.CancellationToken);
        doc.IsPdfaCompliant.ShouldBeTrue();

        await Processor.RemovePdfaComplianceAsync(doc, TestContext.Current.CancellationToken);

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.IsPdfaCompliant.ShouldBeFalse();
    }

    // ── RemovePdfUaCompliance ─────────────────────────────────────────────────

    [Fact]
    public async Task RemovePdfUaCompliance_TaggedDocument_IsTaggedReturnsFalse()
    {
        await using var source = await Processor.LoadFromTxtAsync(
            "Hello",
            new TxtLoadOptions(Tagged: true, Language: "en"),
            TestContext.Current.CancellationToken);
        using var tagged = new MemoryStream();
        await Processor.SaveAsync(source, tagged, ct: TestContext.Current.CancellationToken);

        await using var doc = await LoadAsync(tagged.ToArray(), TestContext.Current.CancellationToken);
        doc.IsTagged.ShouldBeTrue();

        await Processor.RemovePdfUaComplianceAsync(doc, TestContext.Current.CancellationToken);

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.IsTagged.ShouldBeFalse();
    }

    // ── EmbedStandardFonts ────────────────────────────────────────────────────

    [Fact]
    public async Task EmbedStandardFonts_EmptyFontMap_DoesNotThrow()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Processor.EmbedStandardFontsAsync(
            doc,
            new Dictionary<string, byte[]>(),
            TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task EmbedStandardFonts_WithFontBytes_InjectsFontFile2Entry()
    {
        var fakeFont = new byte[256];
        var fontMap = new Dictionary<string, byte[]> { ["Helvetica"] = fakeFont };

        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello",
            ct: TestContext.Current.CancellationToken);
        await Processor.EmbedStandardFontsAsync(doc, fontMap, TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        Encoding.Latin1.GetString(ms.ToArray()).ShouldContain("/FontFile2");
    }

    // ── OptimizeSize + AllowReusePageContent SaveOptions ──────────────────────

    [Fact]
    public async Task SaveWithOptimizeSize_ProducesLoadablePdf()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, new SaveOptions(OptimizeSize: true), TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task SaveWithAllowReusePageContent_ProducesLoadablePdf()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, new SaveOptions(AllowReusePageContent: true), TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task SaveCompactPreset_ProducesLoadablePdf()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, SaveOptions.Compact, TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(2);
    }

    // ── IgnoreCorruptedObjects ────────────────────────────────────────────────

    [Fact]
    public async Task DocumentProcessor_IgnoreCorruptedObjects_LoadsNormalPdfSuccessfully()
    {
        var processor = new Engine.DocumentProcessor(ignoreCorruptedObjects: true);
        await using var doc = await processor.LoadAsync(new MemoryStream(PdfFixtures.SinglePage()), TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }
}
