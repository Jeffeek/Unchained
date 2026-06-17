using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Drawing.Tests.Decoders;

/// <summary>
///     Tests for <see cref="JpxDecoder" />. Malformed JPEG 2000 input is wrapped in an
///     <see cref="InvalidOperationException" /> rather than surfacing the underlying codec failure;
///     valid 1- and 3-component codestreams decode to a flat RGB buffer (grayscale expanded to
///     R=G=B). The JP2 fixtures are CoreJ2K-encoded 8×6 images held as base64 so the success path is
///     exercised deterministically without shipping binary assets.
/// </summary>
public sealed class JpxDecoderTests
{
    // 8×6 single-component (grayscale) JP2, CoreJ2K-encoded.
    private const string Gray8X6Base64 =
        "AAAADGpQICANCocKAAAAFGZ0eXBqcDIgAAAAAGpwMiAAAAAtanAyaAAAABZpaGRyAAAABgAAAAgAAQcHAQAAAAAPY29scgEAAAAAABEAAADFanAyY/9P/1EAKQAAAAAACAAAAAYAAAAAAAAAAAAAAAgAAAAGAAAAAAAAAAAAAQcBAf9SAAwAAQABAAUEBAAA/1wAI0JvGG7qbupuvGcAZwBm4l9MX0xfZEgDSANIRU/ST9JPYf+QAAoAAAAAAFsAAf+TwPrAgAhLAADAYMPoBQPoBAEBwgXIw+oGh9QHD6gMEIrbnqRACqZuC3R7w+oHj7QuH1BAGzWl5hCHHA45msPaYqlFBD7IGbsfAR5mwPr/2Q==";

    // 8×6 three-component (RGB) JP2, CoreJ2K-encoded.
    private const string Rgb8X6Base64 =
        "AAAADGpQICANCocKAAAAFGZ0eXBqcDIgAAAAAGpwMiAAAAAtanAyaAAAABZpaGRyAAAABgAAAAgAAwcHAQAAAAAPY29scgEAAAAAABAAAAD2anAyY/9P/1EALwAAAAAACAAAAAYAAAAAAAAAAAAAAAgAAAAGAAAAAAAAAAAAAwcBAQcBAQcBAf9SAAwAAQABAQUEBAAA/1wAI0JvGG7qbupuvGcAZwBm4l9MX0xfZEgDSANIRU/ST9JPYf+QAAoAAAAAAIYAAf9SAAwAAQABAQUEBAAA/5PH8gQFfsH4gQABYMH5gQAGFwAAAAAAAMB84KH4AQAECASPwDwMH2gQBASIwD5AMD6AQAcJfcB4GAdCAAwIRMA6DAEIDAjAOhQBCA4HAcARQPkCgBvHDL9IaTGgPhEADL9IZqA+EQAOPykz/9k=";

    [Fact]
    public void Decode_GarbageData_ThrowsInvalidOperation() =>
        Should.Throw<InvalidOperationException>(static () =>
            JpxDecoder.Decode(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 })
        );

    [Fact]
    public void Decode_Empty_Throws() =>
        Should.Throw<Exception>(static () => JpxDecoder.Decode(ReadOnlyMemory<byte>.Empty));

    [Fact]
    public void Decode_GrayscaleImage_ExpandsToRgb()
    {
        var rgb = JpxDecoder.Decode(Convert.FromBase64String(Gray8X6Base64));

        // 8×6 pixels × 3 bytes per pixel.
        rgb.Length.ShouldBe(8 * 6 * 3);

        // Grayscale is expanded R=G=B for every pixel.
        var span = rgb.Span;
        for (var i = 0; i < 8 * 6; i++)
        {
            span[i * 3].ShouldBe(span[(i * 3) + 1]);
            span[(i * 3) + 1].ShouldBe(span[(i * 3) + 2]);
        }
    }

    [Fact]
    public void Decode_RgbImage_ProducesRgbBuffer()
    {
        var rgb = JpxDecoder.Decode(Convert.FromBase64String(Rgb8X6Base64));

        rgb.Length.ShouldBe(8 * 6 * 3);

        // A colour image should not be uniformly grey across every channel.
        var span = rgb.Span;
        var anyChannelDiffers = false;
        for (var i = 0; i < 8 * 6 && !anyChannelDiffers; i++)
        {
            if (span[i * 3] != span[(i * 3) + 1] || span[(i * 3) + 1] != span[(i * 3) + 2])
                anyChannelDiffers = true;
        }

        anyChannelDiffers.ShouldBeTrue();
    }
}
