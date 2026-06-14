using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class PdfAValidationResultTests
{
    [Fact]
    public async Task IsConformant_NoViolations_ReturnsTrue()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA1B,
            Violations = []
        };
        result.IsConformant.ShouldBeTrue();
    }

    [Fact]
    public async Task IsConformant_OnlyWarnings_ReturnsTrue()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA2B,
            Violations =
            [
                new PdfAViolation("6.1.1", "Optional metadata missing", PdfAViolationSeverity.Warning)
            ]
        };
        result.IsConformant.ShouldBeTrue();
    }

    [Fact]
    public async Task IsConformant_HasError_ReturnsFalse()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA1B,
            Violations =
            [
                new PdfAViolation("6.3.3", "Font not embedded")
            ]
        };
        result.IsConformant.ShouldBeFalse();
    }

    [Fact]
    public async Task Errors_FiltersOnlyErrors()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA1B,
            Violations =
            [
                new PdfAViolation("6.3.3", "Font not embedded"),
                new PdfAViolation("6.1.1", "Optional metadata", PdfAViolationSeverity.Warning),
                new PdfAViolation("6.2.1", "Color space issue")
            ]
        };
        result.Errors.Count.ShouldBe(2);
        result.Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ToString_Conformant_IncludesProfile()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA1B,
            Violations = []
        };
        var str = result.ToString();
        str.ShouldContain("Conformant");
        str.ShouldContain("0 warning(s)");
    }

    [Fact]
    public async Task ToString_NonConformant_IncludesErrorAndWarningCounts()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA2B,
            Violations =
            [
                new PdfAViolation("6.3.3", "Font not embedded"),
                new PdfAViolation("6.1.1", "Optional metadata", PdfAViolationSeverity.Warning)
            ]
        };
        var str = result.ToString();
        str.ShouldContain("Non-conformant");
        str.ShouldContain("1 error(s)");
        str.ShouldContain("1 warning(s)");
    }

    [Fact]
    public async Task ToString_ConformantWithWarnings_IncludesWarningCount()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA1B,
            Violations =
            [
                new PdfAViolation("6.1.1", "Warn1", PdfAViolationSeverity.Warning),
                new PdfAViolation("6.1.2", "Warn2", PdfAViolationSeverity.Warning)
            ]
        };
        var str = result.ToString();
        str.ShouldContain("Conformant");
        str.ShouldContain("2 warning(s)");
    }
}
