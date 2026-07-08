using System.Text;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests for PDF linearization (ISO 32000-1 Annex F / web-optimized output).
///     Verifies structural constraints without requiring an external PDF tool.
/// </summary>
public sealed class LinearizationTests : PdfTestBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<byte[]> LinearizeAsync(byte[] source, CancellationToken ct = default)
    {
        await using var doc = await LoadAsync(source, ct);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, new SaveOptions(Linearize: true), ct);
        return ms.ToArray();
    }

    private static Task<byte[]> LinearizeDocAsync(int pageCount, CancellationToken ct = default) =>
        LinearizeAsync(PdfFixtures.MultiPage(pageCount), ct);

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Linearize_SinglePage_RoundTripsSuccessfully()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);

        await using var doc = await LoadAsync(bytes, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task Linearize_MultiPage_RoundTripsSuccessfully()
    {
        var bytes = await LinearizeDocAsync(5, TestContext.Current.CancellationToken);

        await using var doc = await LoadAsync(bytes, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(5);
    }

    [Fact]
    public async Task Linearize_TenPage_RoundTripsSuccessfully()
    {
        var bytes = await LinearizeDocAsync(10, TestContext.Current.CancellationToken);

        await using var doc = await LoadAsync(bytes, TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(10);
    }

    // ── PDF header ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Linearize_Output_StartsWithPdfHeader()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);

        var header = Encoding.ASCII.GetString(bytes, 0, 8);
        header.ShouldStartWith("%PDF-", customMessage: "Linearized output must start with a valid PDF header.");
    }

    // ── Linearization parameter dictionary ───────────────────────────────────

    [Fact]
    public async Task Linearize_Output_ContainsLinearizedEntry()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        text.ShouldContain("/Linearized", customMessage: "Output must contain the /Linearized linearization parameter dictionary.");
    }

    [Fact]
    public async Task Linearize_LinearizationDict_IsInFirst1024Bytes()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);

        // ISO 32000-1 Annex F §F.3.1: readers scan the first 1024 bytes for the linearization dict.
        var preamble = Encoding.Latin1.GetString(bytes, 0, Math.Min(1024, bytes.Length));
        preamble.ShouldContain("/Linearized", customMessage: "Linearization parameter dictionary must appear in the first 1024 bytes.");
    }

    [Fact]
    public async Task Linearize_LinearizationDict_ContainsFileLengthEntry()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);
        var preamble = Encoding.Latin1.GetString(bytes, 0, Math.Min(1024, bytes.Length));

        preamble.ShouldContain("/L ", customMessage: "Linearization parameter dict must contain /L (file length).");
    }

    [Fact]
    public async Task Linearize_LinearizationDict_FileLengthMatchesActualFileSize()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);
        var preamble = Encoding.Latin1.GetString(bytes, 0, Math.Min(1024, bytes.Length));

        // Extract /L value.
        var lIndex = preamble.IndexOf("/L ", StringComparison.Ordinal);
        lIndex.ShouldBeGreaterThanOrEqualTo(0, "/L entry must be present.");

        var afterL = preamble[(lIndex + 3)..].TrimStart();
        var end = afterL.IndexOfAny([' ', '\n', '\r', '>']);
        var lValueStr = end >= 0 ? afterL[..end] : afterL;
        long.TryParse(lValueStr, out var lValue).ShouldBeTrue($"Could not parse /L value from '{lValueStr}'.");

        lValue.ShouldBe(bytes.Length, "/L must equal the actual file length.");
    }

    [Fact]
    public async Task Linearize_LinearizationDict_ContainsPageCountEntry()
    {
        var bytes = await LinearizeDocAsync(3, TestContext.Current.CancellationToken);
        var preamble = Encoding.Latin1.GetString(bytes, 0, Math.Min(1024, bytes.Length));

        preamble.ShouldContain("/N ", customMessage: "Linearization parameter dict must contain /N (page count).");
    }

    [Fact]
    public async Task Linearize_LinearizationDict_ContainsHintStreamEntry()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);
        var preamble = Encoding.Latin1.GetString(bytes, 0, Math.Min(1024, bytes.Length));

        preamble.ShouldContain("/H [", customMessage: "Linearization parameter dict must contain /H hint stream offset+length array.");
    }

    [Fact]
    public async Task Linearize_LinearizationDict_ContainsFirstPageObjectEntry()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);
        var preamble = Encoding.Latin1.GetString(bytes, 0, Math.Min(1024, bytes.Length));

        preamble.ShouldContain("/O ", customMessage: "Linearization parameter dict must contain /O (first page object number).");
    }

    [Fact]
    public async Task Linearize_LinearizationDict_ContainsEndOfFirstPageEntry()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);
        var preamble = Encoding.Latin1.GetString(bytes, 0, Math.Min(1024, bytes.Length));

        preamble.ShouldContain("/E ", customMessage: "Linearization parameter dict must contain /E (end of first-page section offset).");
    }

    [Fact]
    public async Task Linearize_LinearizationDict_ContainsMainXrefOffsetEntry()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);
        var preamble = Encoding.Latin1.GetString(bytes, 0, Math.Min(1024, bytes.Length));

        preamble.ShouldContain("/T ", customMessage: "Linearization parameter dict must contain /T (main xref offset).");
    }

    // ── Hint stream ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Linearize_Output_ContainsHintStream()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        // Hint stream has /Filter /FlateDecode per our implementation.
        text.ShouldContain("FlateDecode", customMessage: "Hint stream must be FlateDecode-compressed.");
    }

    // ── xref structure ────────────────────────────────────────────────────────

    [Fact]
    public async Task Linearize_Output_ContainsTwoXrefSections()
    {
        var bytes = await LinearizeDocAsync(2, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        var count = CountOccurrences(text, "xref\n");
        count.ShouldBeGreaterThanOrEqualTo(2, "A linearized PDF must have at least two xref sections.");
    }

    [Fact]
    public async Task Linearize_Output_ContainsTwoEofMarkers()
    {
        var bytes = await LinearizeDocAsync(2, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes);

        var count = CountOccurrences(text, "%%EOF");
        count.ShouldBeGreaterThanOrEqualTo(2, "A linearized PDF must have %%EOF after the first-page section and at the end.");
    }

    [Fact]
    public async Task Linearize_Output_EndsWithEofMarker()
    {
        var bytes = await LinearizeDocAsync(1, TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes).TrimEnd();

        text.ShouldEndWith("%%EOF", customMessage: "Linearized PDF must end with %%EOF.");
    }

    // ── SaveOptions.WebOptimized ──────────────────────────────────────────────

    [Fact]
    public async Task WebOptimized_StaticPreset_ProducesLinearizedOutput()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, SaveOptions.WebOptimized, TestContext.Current.CancellationToken);

        var bytes = ms.ToArray();
        var preamble = Encoding.Latin1.GetString(bytes, 0, Math.Min(1024, bytes.Length));
        preamble.ShouldContain("/Linearized", customMessage: "SaveOptions.WebOptimized must produce a linearized PDF.");
    }

    [Fact]
    public async Task WebOptimized_AndExplicitLinearize_ProduceEquivalentStructure()
    {
        var source = PdfFixtures.SinglePage();

        await using var doc1 = await LoadAsync(source, TestContext.Current.CancellationToken);
        using var ms1 = new MemoryStream();
        await Processor.SaveAsync(doc1, ms1, SaveOptions.WebOptimized, TestContext.Current.CancellationToken);

        await using var doc2 = await LoadAsync(source, TestContext.Current.CancellationToken);
        using var ms2 = new MemoryStream();
        await Processor.SaveAsync(doc2, ms2, new SaveOptions(Linearize: true), TestContext.Current.CancellationToken);

        // Both must be parseable and have the same page count.
        await using var r1 = await LoadAsync(ms1.ToArray(), TestContext.Current.CancellationToken);
        await using var r2 = await LoadAsync(ms2.ToArray(), TestContext.Current.CancellationToken);
        r1.PageCount.ShouldBe(r2.PageCount);
    }

    // ── Non-linearized path unaffected ────────────────────────────────────────

    [Fact]
    public async Task NonLinearized_Save_DoesNotContainLinearizedEntry()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, SaveOptions.Default, TestContext.Current.CancellationToken);

        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldNotContain("/Linearized", customMessage: "Non-linearized save must not contain /Linearized entry.");
    }

    // ── Content preservation ──────────────────────────────────────────────────

    [Fact]
    public async Task Linearize_TextDocument_PreservesExtractedText()
    {
        await using var source = await Processor.LoadFromTxtAsync(
            "Hello linearized world",
            ct: TestContext.Current.CancellationToken
        );
        using var ms = new MemoryStream();
        await Processor.SaveAsync(source, ms, new SaveOptions(Linearize: true), TestContext.Current.CancellationToken);

        await using var reloaded = await LoadAsync(ms.ToArray(), TestContext.Current.CancellationToken);
        var text = reloaded.Pages[1].ExtractText();
        text.ShouldContain("Hello", customMessage: "Text content must be preserved through linearization.");
    }

    [Fact]
    public async Task Linearize_MultiPageDocument_AllPagesAccessible()
    {
        var bytes = await LinearizeDocAsync(4, TestContext.Current.CancellationToken);
        await using var doc = await LoadAsync(bytes, TestContext.Current.CancellationToken);

        doc.PageCount.ShouldBe(4);
        for (var i = 1; i <= 4; i++)
            doc.Pages[i].ShouldNotBeNull($"Page {i} must be accessible after linearization.");
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
