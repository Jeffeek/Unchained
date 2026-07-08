using Shouldly;
using Unchained.Xlsx.Models;
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
        result.AllowFormatCells.ShouldBeTrue();
        result.AllowInsertRows.ShouldBeTrue();
        result.AllowInsertColumns.ShouldBeTrue();
        result.AllowDeleteRows.ShouldBeTrue();
        result.AllowDeleteColumns.ShouldBeTrue();
        result.AllowSort.ShouldBeTrue();
        result.AllowAutoFilter.ShouldBeTrue();
        result.AllowSelectLockedCells.ShouldBeFalse();
        result.AllowSelectUnlockedCells.ShouldBeFalse();
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
        result.ScaleWithDocument.ShouldBeFalse();
        result.AlignWithMargins.ShouldBeFalse();
        result.OddHeader.ShouldBe("&LOdd");
        result.EvenHeader.ShouldBe("&LEven");
        result.EvenFooter.ShouldBe("&CEvenFoot");
        result.FirstPageHeader.ShouldBe("&CFirst");
        result.FirstPageFooter.ShouldBe("&CFirstFoot");
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
        result.ActiveCell!.Value.ToA1().ShouldBe("C3");
    }

    [Fact]
    public async Task FreezeColumnsOnly_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].FreezePanes(0, 3);

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
            Type = DataValidationType.Decimal,
            Operator = DataValidationOperator.GreaterThan,
            Formula1 = "0.5",
            ErrorStyle = DataValidationErrorStyle.Warning,
            PromptTitle = "Enter value",
            Prompt = "A decimal please",
            ShowInputMessage = true,
            ShowErrorAlert = true
        };
        decimalValidation.Ranges.Add(CellRange.FromA1("A1:A10"));
        sheet.DataValidations.Add(decimalValidation);

        var textLength = new DataValidation.DataValidation
        {
            Type = DataValidationType.TextLength,
            Operator = DataValidationOperator.LessThanOrEqual,
            Formula1 = "10"
        };
        textLength.Ranges.Add(CellRange.FromA1("B1:B10"));
        sheet.DataValidations.Add(textLength);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].DataValidations;
        result.Count.ShouldBe(2);

        var dv1 = result[0];
        dv1.Type.ShouldBe(DataValidationType.Decimal);
        dv1.Operator.ShouldBe(DataValidationOperator.GreaterThan);
        dv1.Formula1.ShouldBe("0.5");
        dv1.ErrorStyle.ShouldBe(DataValidationErrorStyle.Warning);
        dv1.PromptTitle.ShouldBe("Enter value");
        dv1.Prompt.ShouldBe("A decimal please");
        dv1.ShowInputMessage.ShouldBeTrue();
        dv1.ShowErrorAlert.ShouldBeTrue();

        var dv2 = result[1];
        dv2.Type.ShouldBe(DataValidationType.TextLength);
        dv2.Operator.ShouldBe(DataValidationOperator.LessThanOrEqual);
        dv2.Formula1.ShouldBe("10");
    }
}
