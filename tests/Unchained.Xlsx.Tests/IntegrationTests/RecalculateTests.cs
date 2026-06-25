using Shouldly;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class RecalculateTests
{
    [Fact]
    public void Recalculate_FillsCachedValues()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 10.0);
        sheet.SetValue(2, 1, 20.0);
        sheet.SetFormula(3, 1, "=SUM(A1:A2)");
        sheet.SetFormula(4, 1, "=A3*2");

        var count = document.Recalculate();

        count.ShouldBe(2);
        sheet.GetCell(3, 1)!.GetDouble().ShouldBe(30);
        sheet.GetCell(4, 1)!.GetDouble().ShouldBe(60);
    }

    [Fact]
    public async Task Recalculate_ResultsRoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 5.0);
        sheet.SetValue(1, 2, 7.0);
        sheet.SetFormula(1, 3, "=A1+B1");
        document.Recalculate();

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var cell = reloaded.Sheets[0].GetCell(1, 3);
        cell!.FormulaText.ShouldBe("=A1+B1");
        cell.GetDouble().ShouldBe(12);
    }

    [Fact]
    public void Recalculate_TextResult()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, "Hello");
        sheet.SetFormula(2, 1, "=CONCATENATE(A1,\" World\")");
        document.Recalculate();

        sheet.GetCell(2, 1)!.GetString().ShouldBe("Hello World");
    }

    [Fact]
    public void Recalculate_CrossSheetReference()
    {
        using var document = XlsxFixtures.WithSheets("Data", "Summary");
        document.Sheets[0].SetValue(1, 1, 100.0);
        document.Sheets[1].SetFormula(1, 1, "=Data!A1*2");
        document.Recalculate();

        document.Sheets[1].GetCell(1, 1)!.GetDouble().ShouldBe(200);
    }

    [Fact]
    public void Recalculate_CircularReference_YieldsRefError()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        sheet.SetFormula(1, 1, "=B1+1");
        sheet.SetFormula(1, 2, "=A1+1");
        document.Recalculate();

        sheet.GetCell(1, 1)!.GetError().ShouldBe(Unchained.Xlsx.Models.Cell.CellError.Reference);
    }

    [Fact]
    public void Recalculate_ChainedFormulas()
    {
        using var document = XlsxFixtures.WithSheets("S");
        var sheet = document.Sheets[0];
        sheet.SetValue(1, 1, 2.0);
        sheet.SetFormula(2, 1, "=A1*3");   // 6
        sheet.SetFormula(3, 1, "=A2+4");   // 10
        sheet.SetFormula(4, 1, "=A3*A1");  // 20
        document.Recalculate();

        sheet.GetCell(2, 1)!.GetDouble().ShouldBe(6);
        sheet.GetCell(3, 1)!.GetDouble().ShouldBe(10);
        sheet.GetCell(4, 1)!.GetDouble().ShouldBe(20);
    }
}
