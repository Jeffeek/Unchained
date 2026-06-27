using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Shouldly;
using Unchained.Ooxml.Charts;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Validates generated workbooks against the Open XML schema with the official SDK validator.
///     Guards against schema-invalid output (the kind Excel silently repairs by dropping parts).
/// </summary>
public class SchemaValidationTests
{
    private static IReadOnlyList<string> Validate(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(ms, false);
        var validator = new OpenXmlValidator(FileFormatVersions.Office2019);
        // Materialise descriptions while the package is still open.
        return validator.Validate(doc)
            .Select(static e => $"[{e.Id}] {e.Part?.Uri}{e.Path?.XPath}: {e.Description}")
            .ToList();
    }

    private static string Describe(IEnumerable<string> errors) => string.Join("\n", errors);

    [Fact]
    public async Task BlankWorkbook_IsSchemaValid()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetValue(1, 1, "Hello");
        var bytes = await XlsxFixtures.SaveBytesAsync(document);

        var errors = Validate(bytes);
        errors.ShouldBeEmpty(Describe(errors));
    }

    [
        Theory,
        InlineData(ChartType.ColumnClustered),
        InlineData(ChartType.BarStacked),
        InlineData(ChartType.Line),
        InlineData(ChartType.Pie),
        InlineData(ChartType.Doughnut),
        InlineData(ChartType.Area),
        InlineData(ChartType.ScatterWithMarkersOnly),
        InlineData(ChartType.Radar)
    ]
    public async Task ChartWorkbook_IsSchemaValid(ChartType type)
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Cat");
        sheet.SetValue(1, 2, "Val");
        sheet.SetValue(2, 1, "A");
        sheet.SetValue(2, 2, 10.0);
        sheet.SetValue(3, 1, "B");
        sheet.SetValue(3, 2, 20.0);
        sheet.AddChart(
            type,
            CellRange.FromA1("A1:B3"),
            DrawingAnchor.OneCell(CellReference.FromA1("D1")),
            "Title"
        );

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
        var errors = Validate(bytes);
        errors.ShouldBeEmpty(Describe(errors));
    }

    [Fact]
    public async Task ImageWorkbook_IsSchemaValid()
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC"
        );
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].AddImage(png, "image/png", CellReference.FromA1("B2"));

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
        var errors = Validate(bytes);
        errors.ShouldBeEmpty(Describe(errors));
    }

    [Fact]
    public async Task PivotWorkbook_IsSchemaValid()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Region");
        sheet.SetValue(1, 2, "Amount");
        sheet.SetValue(2, 1, "East");
        sheet.SetValue(2, 2, 100.0);
        sheet.SetValue(3, 1, "West");
        sheet.SetValue(3, 2, 200.0);
        var pivot = sheet.AddPivotTable(CellRange.FromA1("A1:B3"), CellReference.FromA1("D1"), "P");
        pivot.AddRowField("Region");
        pivot.AddDataField("Amount");

        var bytes = await XlsxFixtures.SaveBytesAsync(document);
        var errors = Validate(bytes);
        errors.ShouldBeEmpty(Describe(errors));
    }
}
