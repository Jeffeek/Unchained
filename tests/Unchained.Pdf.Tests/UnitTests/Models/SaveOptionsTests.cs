using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class SaveOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        SaveOptions.Default.Version.ShouldBe(PdfVersion.Pdf17);
        SaveOptions.Default.Linearize.ShouldBeFalse();
        SaveOptions.Default.OptimizeImages.ShouldBeFalse();
    }

    [Fact]
    public void WebOptimized_IsLinearized() =>
        SaveOptions.WebOptimized.Linearize.ShouldBeTrue();

    [Fact]
    public void WebOptimized_VersionIsPdf17() =>
        SaveOptions.WebOptimized.Version.ShouldBe(PdfVersion.Pdf17);

    [Fact]
    public void RecordEquality_Works()
    {
        // ReSharper disable RedundantArgumentDefaultValue
        var a = new SaveOptions(PdfVersion.Pdf17, false);
        var b = new SaveOptions(PdfVersion.Pdf17, false);
        // ReSharper restore RedundantArgumentDefaultValue
        a.ShouldBe(b);
    }
}

public sealed class PdfVersionTests
{
    [Fact]
    public void EnumValues_AllDefined()
    {
        Enum.IsDefined(PdfVersion.Pdf14).ShouldBeTrue();
        Enum.IsDefined(PdfVersion.Pdf15).ShouldBeTrue();
        Enum.IsDefined(PdfVersion.Pdf17).ShouldBeTrue();
        Enum.IsDefined(PdfVersion.PdfA1b).ShouldBeTrue();
        Enum.IsDefined(PdfVersion.PdfA2b).ShouldBeTrue();
    }
}
