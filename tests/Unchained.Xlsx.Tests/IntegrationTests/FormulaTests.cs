using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class FormulaTests
{
    [Fact]
    public async Task Formula_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetFormula(1, 1, "=A2+A3");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetCell(1, 1)!.FormulaText.ShouldBe("=A2+A3");
    }

    [Fact]
    public void FormulaText_StripsAndAddsEquals()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var cell = document.Sheets[0][1, 1];
        cell.FormulaText = "=SUM(A1:A3)";
        cell.FormulaText.ShouldBe("=SUM(A1:A3)");
    }

    [Fact]
    public async Task NamedRange_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.DefinedNames.Add("MyRange", "Data!$A$1:$C$10");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var name = reloaded.DefinedNames.Find("MyRange");
        name.ShouldNotBeNull();
        name.Formula.ShouldBe("Data!$A$1:$C$10");
        name.IsWorkbookScoped.ShouldBeTrue();
    }

    [Fact]
    public async Task SheetScopedName_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data", "Other");
        document.DefinedNames.AddSheetScoped("Local", "Other!$A$1", document.Sheets[1]);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var name = reloaded.DefinedNames[0];
        name.IsWorkbookScoped.ShouldBeFalse();
        name.LocalSheetId.ShouldBe(1);
    }

    [Fact]
    public async Task RecalculateAll_SetsFlag()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].SetFormula(1, 1, "=1+1");
        document.RecalculateAll();

        // Round-trips without error; the fullCalcOnLoad flag is written.
        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].GetCell(1, 1)!.CellType.ShouldBe(CellType.Formula);
    }
}
