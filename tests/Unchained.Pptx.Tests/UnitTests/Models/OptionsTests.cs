using Shouldly;
using Unchained.Pptx.Export;
using Unchained.Pptx.Models;
using Unchained.Pptx.Models.Export;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Models;

public sealed class SaveOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var options = SaveOptions.Default;
        options.Conformance.ShouldBe(PptxConformance.Transitional);
        options.Zip64.ShouldBe(Zip64Policy.IfNecessary);
        options.RefreshThumbnail.ShouldBeTrue();
        options.Password.ShouldBeNull();
        options.Progress.ShouldBeNull();
        options.UseOpenXmlEngine.ShouldBeFalse();
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var options = new SaveOptions
        {
            Conformance = PptxConformance.Strict,
            Zip64 = Zip64Policy.IfNecessary,
            RefreshThumbnail = false,
            Password = "secret",
            UseOpenXmlEngine = true
        };
        options.Conformance.ShouldBe(PptxConformance.Strict);
        options.RefreshThumbnail.ShouldBeFalse();
        options.Password.ShouldBe("secret");
        options.UseOpenXmlEngine.ShouldBeTrue();
    }
}

public sealed class OpenOptionsTests
{
    [Fact]
    public void Defaults()
    {
        var options = new OpenOptions();
        options.Password.ShouldBeNull();
        options.IgnoreLoadWarnings.ShouldBeFalse();
        options.WarningCallback.ShouldBeNull();
        options.UseOpenXmlEngine.ShouldBeFalse();
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var warnings = new List<string>();
        var options = new OpenOptions
        {
            Password = "pw",
            IgnoreLoadWarnings = true,
            WarningCallback = warnings.Add,
            UseOpenXmlEngine = true
        };
        options.Password.ShouldBe("pw");
        options.IgnoreLoadWarnings.ShouldBeTrue();
        options.WarningCallback.ShouldNotBeNull();
        options.UseOpenXmlEngine.ShouldBeTrue();

        options.WarningCallback("hello");
        warnings.ShouldContain("hello");
    }
}

public sealed class PdfSaveOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var options = PdfSaveOptions.Default;
        options.Compliance.ShouldBe(PdfCompliance.Pdf17);
        options.IncludeHiddenSlides.ShouldBeFalse();
        options.IncludeNotes.ShouldBeFalse();
        options.JpegQuality.ShouldBe(85);
        options.AccessiblePdf.ShouldBeFalse();
        options.Progress.ShouldBeNull();
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var options = new PdfSaveOptions
        {
            Compliance = PdfCompliance.Pdf15,
            IncludeHiddenSlides = true,
            IncludeNotes = true,
            JpegQuality = 50,
            AccessiblePdf = true
        };
        options.Compliance.ShouldBe(PdfCompliance.Pdf15);
        options.IncludeHiddenSlides.ShouldBeTrue();
        options.IncludeNotes.ShouldBeTrue();
        options.JpegQuality.ShouldBe(50);
        options.AccessiblePdf.ShouldBeTrue();
    }
}
