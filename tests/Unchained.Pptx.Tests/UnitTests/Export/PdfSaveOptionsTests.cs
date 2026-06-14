using Shouldly;
using Unchained.Pptx.Export;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Export;

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
