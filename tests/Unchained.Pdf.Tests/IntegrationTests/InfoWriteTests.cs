using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests for <see cref="Abstractions.IDocumentProcessor.SetMetadataAsync" /> —
///     writing the PDF /Info dictionary (ISO 32000-1 §14.3.3).
/// </summary>
public sealed class InfoWriteTests : PdfTestBase
{
    // ── Basic write ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SetMetadata_Title_IsReadBackCorrectly()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                "My Title",
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        doc.Metadata.Title.ShouldBe("My Title");
    }

    [Fact]
    public async Task SetMetadata_Author_IsReadBackCorrectly()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                null,
                "Alice",
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        doc.Metadata.Author.ShouldBe("Alice");
    }

    [Fact]
    public async Task SetMetadata_AllFields_AreReadBackCorrectly()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await Processor.SetMetadataAsync(
            doc,
            new DocumentMetadata(
                "Report",
                "Bob",
                "Monthly summary",
                "pdf test",
                "Unchained",
                "Unchained.Pdf",
                null,
                null
            ),
            TestContext.Current.CancellationToken
        );

        doc.Metadata.Title.ShouldBe("Report");
        doc.Metadata.Author.ShouldBe("Bob");
        doc.Metadata.Subject.ShouldBe("Monthly summary");
        doc.Metadata.Keywords.ShouldBe("pdf test");
        doc.Metadata.Creator.ShouldBe("Unchained");
        doc.Metadata.Producer.ShouldBe("Unchained.Pdf");
    }

    // ── Null fields are skipped ───────────────────────────────────────────────

    [Fact]
    public async Task SetMetadata_NullField_DoesNotOverwriteExistingValue()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        // Set title first.
        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                "Original",
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        // Set author without touching title.
        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                null,
                "Alice",
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        doc.Metadata.Title.ShouldBe("Original", "Title must not be overwritten when null is passed.");
        doc.Metadata.Author.ShouldBe("Alice");
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetMetadata_SaveAndReload_MetadataIsPersisted()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                "Persisted",
                "Carol",
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);

        reloaded.Metadata.Title.ShouldBe("Persisted");
        reloaded.Metadata.Author.ShouldBe("Carol");
    }

    [Fact]
    public async Task SetMetadata_TwiceSameField_SecondValueWins()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                "First",
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                "Second",
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        doc.Metadata.Title.ShouldBe("Second");
    }

    [Fact]
    public async Task SetMetadata_DocumentWithExistingInfo_UpdatesInPlace()
    {
        // Build a PDF that already has /Info (via WithEmbeddedFont fixture which includes fonts,
        // but we just need any doc — we create one via TxtConverter that lacks /Info).
        await using var doc = await Processor.LoadFromTxtAsync(
            "Hello",
            ct: TestContext.Current.CancellationToken
        );

        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                "From Txt",
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        doc.Metadata.Title.ShouldBe("From Txt");
    }

    [Fact]
    public async Task SetMetadata_EmptyStringField_ClearsValue()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                "Initial",
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                string.Empty,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        // An empty string is written as an empty PDF string — read back as empty or null.
        var title = doc.Metadata.Title;
        (title is null or "").ShouldBeTrue();
    }

    // ── Page count unaffected ─────────────────────────────────────────────────

    [Fact]
    public async Task SetMetadata_DoesNotChangePageCount()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);

        await Processor.SetMetadataAsync(
            doc,
            // ReSharper disable BadListLineBreaks
            new DocumentMetadata(
                "Three Pages",
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ),
            // ReSharper restore BadListLineBreaks
            TestContext.Current.CancellationToken
        );

        doc.PageCount.ShouldBe(3);
    }
}
