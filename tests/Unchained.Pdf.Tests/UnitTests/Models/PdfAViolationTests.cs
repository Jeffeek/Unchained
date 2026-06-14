using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class PdfAViolationTests
{
    [Fact]
    public async Task RequiredConstructor_SetsRuleIdAndDescription()
    {
        await Task.CompletedTask;
        var v = new PdfAViolation("6.3.3", "Font not embedded");
        v.RuleId.ShouldBe("6.3.3");
        v.Description.ShouldBe("Font not embedded");
        v.Severity.ShouldBe(PdfAViolationSeverity.Error);
        v.ObjectNumber.ShouldBeNull();
        v.PageNumber.ShouldBeNull();
    }

    [Fact]
    public async Task OptionalSeverity_Warning()
    {
        await Task.CompletedTask;
        var v = new PdfAViolation("6.1.1", "Metadata issue", PdfAViolationSeverity.Warning);
        v.Severity.ShouldBe(PdfAViolationSeverity.Warning);
    }

    [Fact]
    public async Task ObjectNumber_Stored()
    {
        await Task.CompletedTask;
        var v = new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Error, 42);
        v.ObjectNumber.ShouldBe(42);
        v.PageNumber.ShouldBeNull();
    }

    [Fact]
    public async Task PageNumber_Stored()
    {
        await Task.CompletedTask;
        var v = new PdfAViolation("6.3.3", "Font issue", PageNumber: 3);
        v.PageNumber.ShouldBe(3);
        v.ObjectNumber.ShouldBeNull();
    }

    [Fact]
    public async Task AllParameters_Stored()
    {
        await Task.CompletedTask;
        var v = new PdfAViolation("6.3.3", "Font issue", PdfAViolationSeverity.Warning, 7, 2);
        v.RuleId.ShouldBe("6.3.3");
        v.Description.ShouldBe("Font issue");
        v.Severity.ShouldBe(PdfAViolationSeverity.Warning);
        v.ObjectNumber.ShouldBe(7);
        v.PageNumber.ShouldBe(2);
    }

    [Fact]
    public async Task RecordEquality_SameValues_AreEqual()
    {
        await Task.CompletedTask;
        var a = new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Error, 10, 1);
        var b = new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Error, 10, 1);
        a.ShouldBe(b);
    }

    [Fact]
    public async Task RecordEquality_DifferentSeverity_NotEqual()
    {
        await Task.CompletedTask;
        var a = new PdfAViolation("6.3.3", "Font not embedded");
        var b = new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Warning);
        a.ShouldNotBe(b);
    }

    [Fact]
    public async Task SeverityEnum_HasBothValues()
    {
        await Task.CompletedTask;
        Enum.IsDefined(PdfAViolationSeverity.Error).ShouldBeTrue();
        Enum.IsDefined(PdfAViolationSeverity.Warning).ShouldBeTrue();
    }
}
