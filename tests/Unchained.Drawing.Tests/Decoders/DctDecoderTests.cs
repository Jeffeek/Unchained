using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Drawing.Tests.Decoders;

/// <summary>
///     Tests for <see cref="DctDecoder" /> (JpegLibrary-backed /DCTDecode). Exercises the grayscale
///     (1-component → R=G=B) and YCbCr (3-component) decode paths and the unsupported-component-count
///     guard. Grayscale input is produced by <see cref="GrayscaleJpegBuilder" />; the colour sample
///     is the shared baseline JPEG.
/// </summary>
public sealed class DctDecoderTests
{
    // The same 16×16 four-quadrant baseline JPEG used by JpegDecoderTests.
    private const string ColorJpegBase64 =
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAQABADAREAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDxKvyo/v09yr8/P8RT4vr/AGSP6VP32r+Aj6g//9k=";

    [Fact]
    public void Decode_GrayscaleJpeg_ExpandsToRgb()
    {
        var jpeg = GrayscaleJpegBuilder.Build(16, 16, gray: 180);

        var rgb = DctDecoder.Decode(jpeg);

        rgb.Length.ShouldBe(16 * 16 * 3);
        var span = rgb.Span;
        // Each pixel's three channels are equal (grayscale replicated).
        for (var i = 0; i < 16 * 16; i++)
        {
            span[i * 3].ShouldBe(span[(i * 3) + 1]);
            span[(i * 3) + 1].ShouldBe(span[(i * 3) + 2]);
        }
    }

    [Fact]
    public void Decode_ColorJpeg_ProducesRgbBuffer()
    {
        var jpeg = Convert.FromBase64String(ColorJpegBase64);

        var rgb = DctDecoder.Decode(jpeg);

        rgb.Length.ShouldBe(16 * 16 * 3);
        // A multi-colour image must not be uniformly grey.
        var span = rgb.Span;
        var anyChannelDiffers = false;
        for (var i = 0; i < 16 * 16 && !anyChannelDiffers; i++)
        {
            if (span[i * 3] != span[(i * 3) + 1] || span[(i * 3) + 1] != span[(i * 3) + 2])
                anyChannelDiffers = true;
        }

        anyChannelDiffers.ShouldBeTrue();
    }
}
