using Shouldly;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.DataValidation;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class DataValidationTests
{
    [Fact]
    public async Task DropdownValidation_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        document.Sheets[0].AddDropdownValidation(CellRange.FromA1("B2:B100"), "Yes", "No", "Maybe");

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var validations = reloaded.Sheets[0].DataValidations;
        validations.Count.ShouldBe(1);
        validations[0].Type.ShouldBe(DataValidationType.List);
        validations[0].Formula1.ShouldBe("\"Yes,No,Maybe\"");
        validations[0].Ranges[0].ToA1().ShouldBe("B2:B100");
    }

    [Fact]
    public async Task WholeNumberValidation_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        var validation = new DataValidation.DataValidation
        {
            Type = DataValidationType.Whole,
            Operator = DataValidationOperator.Between,
            Formula1 = "1",
            Formula2 = "100",
            ErrorStyle = DataValidationErrorStyle.Stop,
            ErrorTitle = "Out of range",
            ErrorMessage = "Enter 1-100"
        };
        validation.Ranges.Add(CellRange.FromA1("D2:D100"));
        document.Sheets[0].DataValidations.Add(validation);

        using var reloaded = await XlsxFixtures.RoundTripAsync(document);
        var result = reloaded.Sheets[0].DataValidations[0];
        result.Type.ShouldBe(DataValidationType.Whole);
        result.Formula1.ShouldBe("1");
        result.Formula2.ShouldBe("100");
        result.ErrorTitle.ShouldBe("Out of range");
    }
}
