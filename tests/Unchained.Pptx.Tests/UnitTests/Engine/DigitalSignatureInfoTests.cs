using Shouldly;
using Unchained.Pptx.Engine;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Engine;

public sealed class DigitalSignatureInfoTests
{
    [Fact]
    public void Defaults()
    {
        var info = new DigitalSignatureInfo();
        info.SignerName.ShouldBe(string.Empty);
        info.SigningTime.ShouldBeNull();
        info.PartUri.ShouldBe(string.Empty);
        info.IsReadable.ShouldBeFalse();
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        // ReSharper disable BadListLineBreaks
        var when = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
        // ReSharper restore BadListLineBreaks
        var info = new DigitalSignatureInfo
        {
            SignerName = "CN=Alice",
            SigningTime = when,
            PartUri = "/_xmlsignatures/sig1.xml",
            IsReadable = true
        };
        info.SignerName.ShouldBe("CN=Alice");
        info.SigningTime.ShouldBe(when);
        info.PartUri.ShouldBe("/_xmlsignatures/sig1.xml");
        info.IsReadable.ShouldBeTrue();
    }
}
