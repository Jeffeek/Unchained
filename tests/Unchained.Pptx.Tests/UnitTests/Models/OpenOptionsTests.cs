using Shouldly;
using Unchained.Pptx.Models;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Models;

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
