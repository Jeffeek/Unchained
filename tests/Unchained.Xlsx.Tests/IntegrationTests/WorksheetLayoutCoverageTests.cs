using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.PageSetup;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>Exercises the worksheet layout writer (view, protection, page setup, header/footer) breadth.</summary>
public class WorksheetLayoutCoverageTests
{
    [Fact]
    public async Task SheetProtection_AllPermissions_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var protection = document.Sheets[0].Protection;
        protection.Protect("pw");
        protection.AllowFormatCells = true;
        protection.AllowInsertRows = true;
        protection.AllowInsertColumns = true;
        protection.AllowDeleteRows = true;
        protection.AllowDeleteColumns = true;
        protection.AllowSort = true;
        protection.AllowAutoFilter = true;
        protection.AllowSelectLockedCells = false;
        protection.AllowSelectUnlockedCells = false;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].Protection;
        result.IsProtected.ShouldBeTrue();
        result.AllowSelectLockedCells.ShouldBeFalse();
    }

    [Fact]
    public async Task PageSetup_AllOptions_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var setup = document.Sheets[0].PageSetup;
        setup.PaperSize = 9;
        setup.Scale = 80;
        setup.FitToHeight = 2;
        setup.FirstPageNumber = 5;
        setup.Orientation = PageOrientation.Portrait;
        setup.BlackAndWhite = true;
        setup.Draft = true;
        setup.PrintOrder = PrintOrder.OverThenDown;

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].PageSetup;
        result.PaperSize.ShouldBe(9);
        result.Scale.ShouldBe(80);
        result.FitToHeight.ShouldBe(2);
        result.FirstPageNumber.ShouldBe(5);
        result.Orientation.ShouldBe(PageOrientation.Portrait);
        result.BlackAndWhite.ShouldBeTrue();
        result.Draft.ShouldBeTrue();
        result.PrintOrder.ShouldBe(PrintOrder.OverThenDown);
    }

    [Fact]
    public async Task HeaderFooter_OddEvenFirst_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var hf = document.Sheets[0].HeaderFooter;
        hf.DifferentFirstPage = true;
        hf.DifferentOddEven = true;
        hf.ScaleWithDocument = false;
        hf.AlignWithMargins = false;
        hf.OddHeader = "&LOdd";
        hf.EvenHeader = "&LEven";
        hf.EvenFooter = "&CEvenFoot";
        hf.FirstPageHeader = "&CFirst";
        hf.FirstPageFooter = "&CFirstFoot";

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].HeaderFooter;
        result.DifferentFirstPage.ShouldBeTrue();
        result.DifferentOddEven.ShouldBeTrue();
        result.EvenHeader.ShouldBe("&LEven");
        result.FirstPageHeader.ShouldBe("&CFirst");
    }

    [Fact]
    public async Task SheetView_AllFlags_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var view = document.Sheets[0].View;
        view.ShowGridLines = false;
        view.ShowRowColHeaders = false;
        view.ShowFormulas = true;
        view.ZoomScale = 125;
        view.ActiveCell = CellReference.FromA1("C3");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].View;
        result.ShowGridLines.ShouldBeFalse();
        result.ShowRowColHeaders.ShouldBeFalse();
        result.ShowFormulas.ShouldBeTrue();
        result.ZoomScale.ShouldBe(125);
    }

    [Fact]
    public async Task FreezeColumnsOnly_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].FreezePanes(rows: 0, columns: 3);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var frozen = reloaded.Sheets[0].View.FrozenPanes;
        frozen.ShouldNotBeNull();
        frozen.FrozenColumns.ShouldBe(3);
        frozen.FrozenRows.ShouldBe(0);
    }

    [Fact]
    public async Task DataValidation_AllTypes_RoundTrip()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var sheet = document.Sheets[0];

        var decimalValidation = new DataValidation.DataValidation
        {
            Type = Models.DataValidation.DataValidationType.Decimal,
            Operator = Models.DataValidation.DataValidationOperator.GreaterThan,
            Formula1 = "0.5",
            ErrorStyle = Models.DataValidation.DataValidationErrorStyle.Warning,
            PromptTitle = "Enter value",
            Prompt = "A decimal please",
            ShowInputMessage = true,
            ShowErrorAlert = true
        };
        decimalValidation.Ranges.Add(CellRange.FromA1("A1:A10"));
        sheet.DataValidations.Add(decimalValidation);

        var textLength = new DataValidation.DataValidation
        {
            Type = Models.DataValidation.DataValidationType.TextLength,
            Operator = Models.DataValidation.DataValidationOperator.LessThanOrEqual,
            Formula1 = "10"
        };
        textLength.Ranges.Add(CellRange.FromA1("B1:B10"));
        sheet.DataValidations.Add(textLength);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        reloaded.Sheets[0].DataValidations.Count.ShouldBe(2);
    }
}
