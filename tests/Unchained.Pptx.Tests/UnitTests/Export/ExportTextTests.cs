using Shouldly;
using Unchained.Pptx.Export;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Export;

public sealed class ExportTextTests
{
    [Fact]
    public void EscapeHtml_EscapesAllFiveSpecialCharacters() =>
        ExportText.EscapeHtml("<a href=\"x\" id='y'>&</a>")
            .ShouldBe("&lt;a href=&quot;x&quot; id=&#39;y&#39;&gt;&amp;&lt;/a&gt;");

    [Fact]
    public void EscapeHtml_AmpersandFirst_NoDoubleEscaping() =>
        ExportText.EscapeHtml("a & b").ShouldBe("a &amp; b");

    [Fact]
    public void EscapeHtml_PlainText_Unchanged() =>
        ExportText.EscapeHtml("plain text 123").ShouldBe("plain text 123");

    [Fact]
    public void EscapeHtml_Empty_ReturnsEmpty() =>
        ExportText.EscapeHtml(string.Empty).ShouldBe(string.Empty);

    [Fact]
    public void ToBase64DataUri_BuildsDataUriWithContentTypeAndBase64()
    {
        var data = "hello"u8.ToArray();
        var uri = ExportText.ToBase64DataUri(data, "image/png");
        uri.ShouldBe("data:image/png;base64,aGVsbG8=");
    }

    [Fact]
    public void ToBase64DataUri_Empty_ProducesEmptyPayload() =>
        ExportText.ToBase64DataUri(ReadOnlyMemory<byte>.Empty, "image/jpeg")
            .ShouldBe("data:image/jpeg;base64,");
}
