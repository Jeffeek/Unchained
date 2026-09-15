using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Tests for <see cref="Unchained.Xlsx.Formulas.FormulaCalculator" /> via
///     <c>SpreadsheetDocument.Recalculate()</c>: every cached-result kind (number, boolean, text,
///     error, array → top-left scalar) is stored back onto the formula cell.
/// </summary>
public sealed class FormulaCalculatorTests
{
    [Fact]
    public void Recalculate_StoresEachResultKindOnItsCell()
    {
        using var document = XlsxFixtures.WithSheets("Sheet1");
        var sheet = document.Sheets[0];

        sheet.SetValue(1, 2, 5.0);          // B1
        sheet.SetValue(2, 2, 6.0);          // B2
        sheet.SetFormula(1, 1, "=1+2");     // number
        sheet.SetFormula(2, 1, "=1=1");     // boolean
        sheet.SetFormula(3, 1, "=\"hi\"");  // text
        sheet.SetFormula(4, 1, "=1/0");     // error
        sheet.SetFormula(5, 1, "=B1:B2");   // array → caches top-left scalar

        var evaluated = document.Recalculate();

        evaluated.ShouldBeGreaterThanOrEqualTo(5);
        sheet.GetCell(1, 1)!.GetDouble().ShouldBe(3);
        sheet.GetCell(2, 1)!.GetBoolean().ShouldBe(true);
        sheet.GetCell(3, 1)!.GetString().ShouldBe("hi");
        sheet.GetCell(4, 1)!.GetError().ShouldBe(CellError.DivisionByZero);
        sheet.GetCell(5, 1)!.GetDouble().ShouldBe(5);
    }
}
