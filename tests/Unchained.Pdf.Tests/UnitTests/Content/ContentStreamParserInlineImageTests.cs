using System.Text;
using Shouldly;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Content;

/// <summary>
///     Tests for inline-image (<c>BI</c>…<c>ID</c>…<c>EI</c>) decoding in
///     <see cref="ContentStreamParser" />. Builds content streams with raw inline-image data in
///     several colour spaces and bit depths, asserting the emitted <c>BI</c> operator carries a
///     decoded <see cref="PdfInlineImage" />.
/// </summary>
public sealed class ContentStreamParserInlineImageTests
{
    private static byte[] Build(string header, IEnumerable<byte> imageData, string trailer)
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.Latin1.GetBytes(header));
        bytes.AddRange(imageData);
        bytes.AddRange(Encoding.Latin1.GetBytes(trailer));
        return bytes.ToArray();
    }

    private static PdfInlineImage? FirstInlineImage(byte[] data)
    {
        // The bare `BI` token emits an empty BI operator; the `ID` keyword emits a second BI
        // operator carrying the decoded image. Pick the one with a PdfInlineImage operand.
        var ops = ContentStreamParser.Parse(data);
        return ops
            .Where(static o => o.Name == "BI")
            .SelectMany(static o => o.Operands)
            .OfType<PdfInlineImage>()
            .FirstOrDefault();
    }

    [Fact]
    public void InlineImage_DeviceRgb_2x1_Decodes()
    {
        // 2×1 RGB: red, green.
        byte[] pixels = [255, 0, 0, 0, 255, 0];
        var data = Build("q BI /W 2 /H 1 /CS /RGB /BPC 8 ID ", pixels, " EI Q");

        var image = FirstInlineImage(data);
        image.ShouldNotBeNull();
        image.Width.ShouldBe(2);
        image.Height.ShouldBe(1);
        image.RgbData.Length.ShouldBe(6);
        image.RgbData[0].ShouldBe((byte)255);
    }

    [Fact]
    public void InlineImage_DeviceGray_2x1_ExpandsToRgb()
    {
        byte[] pixels = [100, 200];
        var data = Build("BI /W 2 /H 1 /CS /G /BPC 8 ID ", pixels, " EI");

        var image = FirstInlineImage(data);
        image.ShouldNotBeNull();
        // Gray replicated to R=G=B.
        image.RgbData[0].ShouldBe((byte)100);
        image.RgbData[1].ShouldBe((byte)100);
        image.RgbData[2].ShouldBe((byte)100);
    }

    [Fact]
    public void InlineImage_DeviceCmyk_1x1_ConvertsToRgb()
    {
        // CMYK all-zero = white.
        var pixels = "\0\0\0\0"u8.ToArray();
        var data = Build("BI /W 1 /H 1 /CS /CMYK /BPC 8 ID ", pixels, " EI");

        var image = FirstInlineImage(data);
        image.ShouldNotBeNull();
        image.RgbData.ShouldBe([255, 255, 255]);
    }

    [Fact]
    public void InlineImage_Gray1Bpc_BitPacked_Decodes()
    {
        // 8×1 1-bpc: 0b10000000 → first pixel ink (black), rest paper (white).
        byte[] pixels = [0b1000_0000];
        var data = Build("BI /W 8 /H 1 /CS /G /BPC 1 ID ", pixels, " EI");

        var image = FirstInlineImage(data);
        image.ShouldNotBeNull();
        image.Width.ShouldBe(8);
        // bit=1 → black.
        image.RgbData[0].ShouldBe((byte)0);
        // bit=0 → white.
        image.RgbData[3].ShouldBe((byte)255);
    }

    [Fact]
    public void InlineImage_FullColorSpaceName_Works()
    {
        byte[] pixels = [10, 20, 30];
        var data = Build("BI /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 ID ", pixels, " EI");

        var image = FirstInlineImage(data);
        image.ShouldNotBeNull();
        image.RgbData.ShouldBe([10, 20, 30]);
    }

    [Fact]
    public void InlineImage_ZeroDimensions_NotEmitted()
    {
        var data = Build("BI /W 0 /H 0 /CS /RGB /BPC 8 ID ", (byte[])[], " EI");
        FirstInlineImage(data).ShouldBeNull();
    }

    [Fact]
    public void InlineImage_FollowedByOtherOperators_StreamStaysInSync()
    {
        byte[] pixels = [1, 2, 3];
        var data = Build("BI /W 1 /H 1 /CS /RGB /BPC 8 ID ", pixels, " EI\nBT ET");

        var ops = ContentStreamParser.Parse(data);
        FirstInlineImage(data).ShouldNotBeNull();
        ops.Any(static o => o.Name == "BT").ShouldBeTrue();
        ops.Any(static o => o.Name == "ET").ShouldBeTrue();
    }
}
