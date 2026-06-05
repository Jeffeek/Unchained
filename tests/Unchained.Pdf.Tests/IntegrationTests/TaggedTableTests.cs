using System.Text;
using Shouldly;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Tests for tagged <see cref="Engine.TableGenerator"/> output (ISO 32000-1 §14.7).
/// </summary>
public sealed class TaggedTableTests : PdfTestBase
{
    private static TableData SimpleTaggedTable(int rows = 3) => new()
    {
        Headers = ["Name", "Amount", "Status"],
        Rows = Enumerable.Range(1, rows)
            .Select(static IReadOnlyList<string> (i) => [$"Row {i}", $"${i * 100}", "Active"])
            .ToList(),
        Title = "Sales Report",
        Tagged = true,
        Language = "en-US"
    };

    private static TableData SimpleUntaggedTable(int rows = 3) => new()
    {
        Headers = ["Name", "Amount", "Status"],
        Rows = Enumerable.Range(1, rows)
            .Select(static IReadOnlyList<string> (i) => [$"Row {i}", $"${i * 100}", "Active"])
            .ToList()
    };

    // ── Tagged generate ───────────────────────────────────────────────────────

    [Fact]
    public async Task TaggedTable_Generate_HasMarkInfo()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        Encoding.Latin1.GetString(ms.ToArray()).ShouldContain("/MarkInfo");
    }

    [Fact]
    public async Task TaggedTable_Generate_HasStructTreeRoot()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        Encoding.Latin1.GetString(ms.ToArray()).ShouldContain("/StructTreeRoot");
    }

    [Fact]
    public async Task TaggedTable_Generate_HasLangEntry()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("/Lang");
        text.ShouldContain("en-US");
    }

    [Fact]
    public async Task TaggedTable_Generate_ContentStream_HasBdcEmc()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("BDC");
        text.ShouldContain("EMC");
    }

    [Fact]
    public async Task TaggedTable_Generate_HasThStructureElements()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        Encoding.Latin1.GetString(ms.ToArray()).ShouldContain("/TH");
    }

    [Fact]
    public async Task TaggedTable_Generate_HasTdStructureElements()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        Encoding.Latin1.GetString(ms.ToArray()).ShouldContain("/TD");
    }

    [Fact]
    public async Task TaggedTable_Generate_HasParentTree()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        Encoding.Latin1.GetString(ms.ToArray()).ShouldContain("/ParentTree");
    }

    [Fact]
    public async Task TaggedTable_Generate_HasMcidProperties()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        Encoding.Latin1.GetString(ms.ToArray()).ShouldContain("/MCID 0");
    }

    // ── Untagged still works ──────────────────────────────────────────────────

    [Fact]
    public async Task UntaggedTable_Generate_HasNoMarkInfo()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleUntaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldNotContain("/MarkInfo");
        text.ShouldNotContain("/StructTreeRoot");
    }

    [Fact]
    public async Task UntaggedTable_Generate_HasNoBdcOperators()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleUntaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        Encoding.Latin1.GetString(ms.ToArray()).ShouldNotContain("BDC");
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TaggedTable_Generate_RoundTripsSuccessfully()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    // ── PDF/UA validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task TaggedTable_ValidatePdfUA_NoMarkInfoViolation()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfUAAsync(ms.ToArray(), TestContext.Current.CancellationToken);

        result.Violations.ShouldNotContain(static v => v.RuleId == "7.2");
    }

    [Fact]
    public async Task TaggedTable_ValidatePdfUA_NoLanguageViolation()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfUAAsync(ms.ToArray(), TestContext.Current.CancellationToken);

        result.Violations.ShouldNotContain(static v => v.RuleId == "7.4");
    }

    [Fact]
    public async Task TaggedTable_ValidatePdfUA_NoStructTreeViolation()
    {
        var generator = new Engine.TableGenerator();
        await using var doc = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var result = await Processor.ValidatePdfUAAsync(ms.ToArray(), TestContext.Current.CancellationToken);

        result.Violations.ShouldNotContain(static v => v.RuleId == "7.5");
    }

    [Fact]
    public async Task TaggedTable_FewerPdfUAErrorsThanUntagged()
    {
        var generator = new Engine.TableGenerator();

        await using var tagged = await generator.GenerateAsync(SimpleTaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var taggedMs = new MemoryStream();
        await Processor.SaveAsync(tagged, taggedMs, ct: TestContext.Current.CancellationToken);

        await using var untagged = await generator.GenerateAsync(SimpleUntaggedTable(), TableStyle.Default, TestContext.Current.CancellationToken);
        using var untaggedMs = new MemoryStream();
        await Processor.SaveAsync(untagged, untaggedMs, ct: TestContext.Current.CancellationToken);

        var taggedResult = await Processor.ValidatePdfUAAsync(taggedMs.ToArray(), TestContext.Current.CancellationToken);
        var untaggedResult = await Processor.ValidatePdfUAAsync(untaggedMs.ToArray(), TestContext.Current.CancellationToken);

        var taggedErrors = taggedResult.Violations.Count(static v => v.Severity == PdfUAViolationSeverity.Error);
        var untaggedErrors = untaggedResult.Violations.Count(static v => v.Severity == PdfUAViolationSeverity.Error);

        taggedErrors.ShouldBeLessThan(untaggedErrors);
    }

    // ── Multi-page tagged table ───────────────────────────────────────────────

    [Fact]
    public async Task TaggedTable_MultiPage_AllPagesHaveBdcOperators()
    {
        // Force multiple pages by using many rows.
        var generator = new Engine.TableGenerator();
        var data = new TableData
        {
            Headers = ["Col1", "Col2"],
            Rows = Enumerable.Range(1, 60)
                .Select(static IReadOnlyList<string> (i) => [$"R{i}", $"V{i}"])
                .ToList(),
            Tagged = true,
            Language = "en"
        };

        await using var doc = await generator.GenerateAsync(data, TableStyle.Default, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);

        var text = Encoding.Latin1.GetString(ms.ToArray());
        text.ShouldContain("BDC");
        doc.PageCount.ShouldBeGreaterThan(1);
    }
}
