using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Tests.Helpers;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class ColorTests : PptxTestBase
{
    [Fact]
    public void ColorWriterThenParser_OpaqueRgb_RoundTrips()
    {
        var color = ColorSpec.FromRgb(0x00, 0x70, 0xC0);

        var element = ColorWriter.Write(color);
        // Wrap so ColorParser.Parse can find the child colour element.
        var wrapper = new System.Xml.Linq.XElement("wrap", element);
        var result = ColorParser.Parse(wrapper);

        result.Rgb.ShouldBe(color.Rgb);
    }

    [
        Theory,
        InlineData((byte)0),
        InlineData((byte)64),
        InlineData((byte)128),
        InlineData((byte)200),
        InlineData((byte)254)
    ]
    public void ColorWriterThenParser_AlphaChannel_RoundTrips(byte alpha)
    {
        var color = ColorSpec.FromArgb(alpha, 0x12, 0x34, 0x56);

        var element = ColorWriter.Write(color);
        var wrapper = new System.Xml.Linq.XElement("wrap", element);
        var result = ColorParser.Parse(wrapper);

        var resultAlpha = (byte)((result.Rgb >> 24) & 0xFF);
        // Allow ±1 for the 0–255 ↔ 0–100000 quantisation.
        Math.Abs(resultAlpha - alpha).ShouldBeLessThanOrEqualTo(1);
        (result.Rgb & 0x00FFFFFF).ShouldBe(color.Rgb & 0x00FFFFFF);
    }
}
