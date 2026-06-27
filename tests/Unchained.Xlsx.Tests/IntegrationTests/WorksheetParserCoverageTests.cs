using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Drives <c>WorksheetParser</c> branches that the writer rarely emits (inline strings, the
///     <c>str</c>/<c>b</c>/<c>e</c> cell forms, shared/array formulas, missing reference attributes)
///     by injecting hand-authored sheet XML into a saved workbook and reloading it.
/// </summary>
public class WorksheetParserCoverageTests
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static async Task<SpreadsheetDocument> LoadWithSheetData(string sheetDataInner)
    {
        // Start from a real saved workbook so all other parts are valid, then swap sheet1.xml.
        using var seed = XlsxFixtures.WithSheets("Data");
        var bytes = await XlsxFixtures.SaveBytesAsync(seed);

        var sheetXml =
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{Ns}"><sheetData>{sheetDataInner}</sheetData></worksheet>""";

        using var ms = new MemoryStream();
        await ms.WriteAsync(bytes);
#if NET10_0_OR_GREATER
        await using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, true))
        {
            var entry = archive.GetEntry("xl/worksheets/sheet1.xml");
            entry!.Delete();
            var fresh = archive.CreateEntry("xl/worksheets/sheet1.xml");
            await using var writer = new StreamWriter(await fresh.OpenAsync(), new UTF8Encoding(false));
            await writer.WriteAsync(sheetXml);
        }
#else
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, true))
        {
            var entry = archive.GetEntry("xl/worksheets/sheet1.xml");
            entry!.Delete();
            var fresh = archive.CreateEntry("xl/worksheets/sheet1.xml");
            await using var writer = new StreamWriter(fresh.Open(), new UTF8Encoding(false));
            await writer.WriteAsync(sheetXml);
        }
#endif

        using var processor = new SpreadsheetProcessor();
        return await processor.LoadAsync(ms.ToArray(), cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Parses_InlineString()
    {
        using var document = await LoadWithSheetData(
            """<row r="1"><c r="A1" t="inlineStr"><is><t>inline value</t></is></c></row>"""
        );
        document.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("inline value");
    }

    [Fact]
    public async Task Parses_InlineRichString()
    {
        using var document = await LoadWithSheetData(
            """<row r="1"><c r="A1" t="inlineStr"><is><r><t>foo</t></r><r><t>bar</t></r></is></c></row>"""
        );
        document.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("foobar");
    }

    [Fact]
    public async Task Parses_StrType()
    {
        using var document = await LoadWithSheetData(
            """<row r="1"><c r="A1" t="str"><v>plain</v></c></row>"""
        );
        document.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("plain");
    }

    [Fact]
    public async Task Parses_BooleanAndError()
    {
        using var document = await LoadWithSheetData(
            """<row r="1"><c r="A1" t="b"><v>1</v></c><c r="B1" t="e"><v>#DIV/0!</v></c></row>"""
        );
        var sheet = document.Sheets[0];
        sheet.GetCell(1, 1)!.GetBoolean().ShouldBe(true);
        sheet.GetCell(1, 2)!.GetError().ShouldBe(CellError.DivisionByZero);
    }

    [Fact]
    public async Task Parses_NumberCell()
    {
        using var document = await LoadWithSheetData("""<row r="1"><c r="A1"><v>3.25</v></c></row>""");
        document.Sheets[0].GetCell(1, 1)!.GetDouble().ShouldBe(3.25);
    }

    [Fact]
    public async Task Parses_MissingCellReference_UsesFallback()
    {
        // No 'r' attribute on the cells → parser falls back to sequential columns.
        using var document = await LoadWithSheetData(
            """<row r="2"><c><v>10</v></c><c><v>20</v></c></row>"""
        );
        var sheet = document.Sheets[0];
        sheet.GetCell(2, 1)!.GetDouble().ShouldBe(10);
        sheet.GetCell(2, 2)!.GetDouble().ShouldBe(20);
    }

    [Fact]
    public async Task Parses_MissingRowNumber_UsesFallback()
    {
        using var document = await LoadWithSheetData(
            """<row><c r="A1"><v>5</v></c></row>"""
        );
        document.Sheets[0].GetCell(1, 1)!.GetDouble().ShouldBe(5);
    }

    [Fact]
    public async Task Parses_RowProperties()
    {
        using var document = await LoadWithSheetData(
            """<row r="1" ht="42" customHeight="1" hidden="1" outlineLevel="2"><c r="A1"><v>1</v></c></row>"""
        );
        var row = document.Sheets[0].GetRow(1);
        row.ShouldNotBeNull();
        row.Height.ShouldBe(42);
        row.IsCustomHeight.ShouldBeTrue();
        row.IsHidden.ShouldBeTrue();
        row.OutlineLevel.ShouldBe(2);
    }

    [Fact]
    public async Task Parses_FormulaWithCachedNumber()
    {
        using var document = await LoadWithSheetData(
            """<row r="1"><c r="A1"><f>1+2</f><v>3</v></c></row>"""
        );
        var cell = document.Sheets[0].GetCell(1, 1);
        cell!.CellType.ShouldBe(CellType.Formula);
        cell.FormulaText.ShouldBe("=1+2");
        cell.GetDouble().ShouldBe(3);
    }

    [Fact]
    public async Task Parses_FormulaWithCachedString()
    {
        using var document = await LoadWithSheetData(
            """<row r="1"><c r="A1" t="str"><f>"a"&amp;"b"</f><v>ab</v></c></row>"""
        );
        document.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("ab");
    }

    [Fact]
    public async Task Parses_ArrayFormula()
    {
        using var document = await LoadWithSheetData(
            """<row r="1"><c r="A1"><f t="array" ref="A1:A2">SUM(B1:B2)</f><v>0</v></c></row>"""
        );
        var cell = document.Sheets[0].GetCell(1, 1);
        cell!.IsArrayFormula.ShouldBeTrue();
        cell.ArrayFormulaRange.ShouldNotBeNull();
    }

    [Fact]
    public async Task Parses_SharedFormula_MasterAndContinuation()
    {
        using var document = await LoadWithSheetData(
            """<row r="1"><c r="A1"><f t="shared" si="0" ref="A1:A2">B1*2</f><v>0</v></c></row><row r="2"><c r="A2"><f t="shared" si="0"/><v>0</v></c></row>"""
        );
        var sheet = document.Sheets[0];
        sheet.GetCell(1, 1)!.FormulaText.ShouldBe("=B1*2");
        // Continuation: master shifted down one row → B2*2.
        sheet.GetCell(2, 1)!.FormulaText.ShouldBe("=B2*2");
    }

    [Fact]
    public async Task Parses_EmptySheetData()
    {
        using var document = await LoadWithSheetData(string.Empty);
        document.Sheets[0].GetUsedRange().ShouldBeNull();
    }
}
