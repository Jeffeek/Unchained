using System.IO.Compression;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Coverage for tab colour, RowCollection helpers, and pivot-cache from formula cells.</summary>
public class MiscCoverageTests
{
    // ── Tab colour (WorksheetWriter) ─────────────────────────────────────────

    [Fact]
    public async Task TabColor_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].TabColor = ColorSpec.FromRgb(0xFF, 0x00, 0x00);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].TabColor.ShouldNotBeNull();
    }

    [Fact]
    public async Task TabColor_Set_EmitsSheetPr()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].TabColor = ColorSpec.FromRgb(0x00, 0x80, 0x00);

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
#if NET10_0_OR_GREATER
        await using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#else
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#endif
        var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
#if NET10_0_OR_GREATER
        using var reader = new StreamReader(await entry.OpenAsync());
#else
        using var reader = new StreamReader(entry.Open());
#endif
        var xml = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        xml.ShouldContain("tabColor");
    }

    [Fact]
    public async Task TabColor_Cleared_RoundTripsWithoutColor()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].TabColor = ColorSpec.FromRgb(0xFF, 0x00, 0x00);

        // Round-trip once (writes the colour), then clear and round-trip again.
        using var once = await XlsxFixtures.RoundTripAsync(document);
        once.Sheets[0].TabColor = null;

        using var twice = await XlsxFixtures.RoundTripAsync(once);
        twice.Sheets[0].TabColor.ShouldBeNull();
    }

    // ── RowCollection helpers ────────────────────────────────────────────────

    [Fact]
    public void Rows_GetRowsInRange_FiltersByNumber()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 1.0);
        sheet.SetValue(3, 1, 3.0);
        sheet.SetValue(5, 1, 5.0);

        var rows = sheet.Rows.GetRowsInRange(2, 4).ToList();
        rows.Count.ShouldBe(1);
        rows[0].RowNumber.ShouldBe(3);
    }

    [Fact]
    public void Rows_GetOrCreateRow_IsIdempotent()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var rows = document.Sheets[0].Rows;
        var first = rows.GetOrCreateRow(2);
        var second = rows.GetOrCreateRow(2);

        second.ShouldBeSameAs(first);
        rows.GetRow(2).ShouldBe(first);
        rows.GetRow(99).ShouldBeNull();
    }

    [Fact]
    public void Rows_IndexerAndCount()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 1.0);
        sheet.SetValue(2, 1, 2.0);

        sheet.Rows.Count.ShouldBe(2);
        sheet.Rows[0].RowNumber.ShouldBe(1);
        sheet.Rows.AsEnumerable().Count().ShouldBe(2);
    }

    // ── PivotCacheValue from formula cells ───────────────────────────────────

    [Fact]
    public async Task PivotCache_FromFormulaCells()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Key");
        sheet.SetValue(1, 2, "Value");
        sheet.SetValue(2, 1, "a");
        // A formula cell with a cached numeric result becomes a Number cache value.
        sheet.SetFormulaWithCache(2, 2, "=1+1", 2);
        sheet.SetValue(3, 1, "b");
        sheet.SetValue(3, 2, 5.0);

        var pivot = sheet.AddPivotTable(CellRange.FromA1("A1:B3"), CellReference.FromA1("E1"), "P");
        pivot.AddRowField("Key");
        pivot.AddDataField("Value");

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
#if NET10_0_OR_GREATER
        await using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#else
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
#endif
        archive.Entries.Any(static e => e.FullName.Contains("pivotCacheRecords")).ShouldBeTrue();
    }
}
