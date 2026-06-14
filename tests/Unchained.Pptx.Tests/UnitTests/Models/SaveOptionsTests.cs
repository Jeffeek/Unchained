using Shouldly;
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
