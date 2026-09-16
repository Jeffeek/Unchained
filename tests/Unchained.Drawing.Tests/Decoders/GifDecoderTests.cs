using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Drawing.Tests.Decoders;

/// <summary>
///     Tests for <see cref="GifDecoder" /> — GIF89a signature detection and single-frame decoding,
///     including the reject paths (truncated, wrong signature, zero size, trailer, unknown block).
/// </summary>
public sealed class GifDecoderTests
{
    // A canonical 1×1 GIF89a: header + logical screen descriptor + 2-colour global colour
    // table (black, white) + graphic-control extension + image descriptor + LZW data + trailer.
    private static readonly byte[] OnePixelGif =
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, // "GIF89a"
        0x01, 0x00, 0x01, 0x00,             // width=1, height=1
        0x80, 0x00, 0x00,                   // GCT present, size 2; bg=0; aspect=0
        0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, // GCT: black, white
        0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, // graphic-control extension
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, // image descriptor
        0x02, 0x02, 0x44, 0x01, 0x00,       // LZW min-code-size 2 + one sub-block + terminator
        0x3B                                // trailer
    ];

    [
        Theory,
        InlineData(true, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }),
        InlineData(false, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }), // GIF87a unsupported
        InlineData(false, new byte[] { 0x47, 0x49, 0x46 }),                    // too short
        InlineData(false, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A })   // PNG signature
    ]
    public void IsGif_MatchesOnlyGif89aSignature(bool expected, byte[] bytes) =>
        GifDecoder.IsGif(bytes).ShouldBe(expected);

    [Fact]
    public void TryDecodeToRgb_OnePixelGif_ReturnsSingleRgbTriple()
    {
        var rgb = GifDecoder.TryDecodeToRgb(OnePixelGif, out var width, out var height);

        rgb.ShouldNotBeNull();
        width.ShouldBe(1);
        height.ShouldBe(1);
        rgb.Length.ShouldBe(3);
    }

    [Fact]
    public void TryDecodeToRgb_TooShort_ReturnsNull()
    {
        var rgb = GifDecoder.TryDecodeToRgb("GIF"u8, out var width, out var height);

        rgb.ShouldBeNull();
        width.ShouldBe(0);
        height.ShouldBe(0);
    }

    [Fact]
    public void TryDecodeToRgb_NotAGif_ReturnsNull() =>
        GifDecoder.TryDecodeToRgb(new byte[13], out _, out _).ShouldBeNull();

    [Fact]
    public void TryDecodeToRgb_ZeroDimensions_ReturnsNull()
    {
        // Valid signature but width = height = 0.
        var bytes = "GIF89a\0\0\0\0\0\0\0"u8.ToArray();

        GifDecoder.TryDecodeToRgb(bytes, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void TryDecodeToRgb_TrailerBeforeImage_ReturnsNull()
    {
        // Signature, 1×1, no GCT (flags=0), then an immediate trailer block.
        byte[] bytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x3B];

        GifDecoder.TryDecodeToRgb(bytes, out _, out _).ShouldBeNull();
    }

    [Fact]
    public void TryDecodeToRgb_UnknownBlock_ReturnsNull()
    {
        // Signature, 1×1, no GCT, then an unrecognised block marker.
        byte[] bytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0xAB];

        GifDecoder.TryDecodeToRgb(bytes, out _, out _).ShouldBeNull();
    }
}
