using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class PdfUAValidationResultTests
{
    [Fact]
    public void Default_NoViolations_IsConformant()
    {
        var result = new PdfUAValidationResult();
        result.Violations.ShouldBeEmpty();
        result.IsConformant.ShouldBeTrue();
    }

    [Fact]
    public void WithErrorViolation_IsNotConformant()
    {
        var result = new PdfUAValidationResult
        {
            Violations = [new PdfUAViolation("7.1", "Bad version", PdfUAViolationSeverity.Error)]
        };
        result.IsConformant.ShouldBeFalse();
    }

    [Fact]
    public void WithOnlyWarnings_IsConformant()
    {
        var result = new PdfUAValidationResult
        {
            Violations =
            [
                new PdfUAViolation("7.2", "Maybe", PdfUAViolationSeverity.Warning),
                new PdfUAViolation("7.3", "Another", PdfUAViolationSeverity.Warning)
            ]
        };
        result.IsConformant.ShouldBeTrue();
    }

    [Fact]
    public void Violation_StoresAllFields()
    {
        var violation = new PdfUAViolation("7.5", "Missing struct tree", PdfUAViolationSeverity.Error, 12, 3);
        violation.RuleId.ShouldBe("7.5");
        violation.Description.ShouldBe("Missing struct tree");
        violation.Severity.ShouldBe(PdfUAViolationSeverity.Error);
        violation.ObjectNumber.ShouldBe(12);
        violation.PageNumber.ShouldBe(3);
    }

    [Fact]
    public void Violation_OptionalFields_DefaultToNull()
    {
        var violation = new PdfUAViolation("7.4", "No lang", PdfUAViolationSeverity.Warning);
        violation.ObjectNumber.ShouldBeNull();
        violation.PageNumber.ShouldBeNull();
    }
}
