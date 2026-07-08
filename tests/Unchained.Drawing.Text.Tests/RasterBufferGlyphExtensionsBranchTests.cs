using System.Reflection;
using System.Runtime.InteropServices;
using Shouldly;
using Unchained.Drawing.Text.Extensions;
using Xunit;

namespace Unchained.Drawing.Text.Tests;

/// <summary>
///     Branch coverage for <see cref="RasterBufferGlyphExtensions" />'s low-level
///     <c>BlitFromNativeBuffer</c>. The public <c>BlitGlyphFromFace</c> entry point only ever
///     produces 8-bit gray bitmaps with positive pitch from the bundled fonts, so the
///     1-bit mono path, the negative-pitch (bottom-up) branch, the oversize-dimension guard,
///     and the unknown-pixel-mode arm are exercised here by invoking the private method with
///     hand-built native pixel buffers via reflection.
/// </summary>
public sealed class RasterBufferGlyphExtensionsBranchTests
{
    private const int PixelModeGray = 2;
    private const int PixelModeMono = 1;

    private static readonly MethodInfo BlitMethod =
        typeof(RasterBufferGlyphExtensions).GetMethod(
            "BlitFromNativeBuffer",
            BindingFlags.NonPublic | BindingFlags.Static
        )
        ?? throw new InvalidOperationException("BlitFromNativeBuffer not found");

    // Invokes the private static blitter with a pinned native copy of pixelBytes.
    private static void Blit(
        RasterBuffer buffer,
        int destX,
        int destY,
        int w,
        int h,
        int pitch,
        byte[]? pixelBytes,
        int pixelMode,
        byte r = 0,
        byte g = 0,
        byte b = 0
    )
    {
        var ptr = IntPtr.Zero;
        try
        {
            if (pixelBytes is not null)
            {
                ptr = Marshal.AllocHGlobal(pixelBytes.Length);
                Marshal.Copy(pixelBytes, 0, ptr, pixelBytes.Length);
            }

            BlitMethod.Invoke(
                null,
                [buffer, destX, destY, w, h, pitch, ptr, pixelMode, r, g, b, "Normal"]
            );
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
        }
    }

    private static int CountDark(RasterBuffer buffer)
    {
        var dark = 0;
        for (var y = 0; y < buffer.Height; y++)
        for (var x = 0; x < buffer.Width; x++)
        {
            var (r, g, b) = buffer.GetPixelRgb(x, y);
            if (r < 128 && g < 128 && b < 128) dark++;
        }

        return dark;
    }

    [Fact]
    public void GrayPath_PositivePitch_BlitsCoverage()
    {
        var buffer = new RasterBuffer(8, 4);
        buffer.Clear();
        // 4×2 gray glyph, full coverage (255), pitch == width.
        var pixels = new byte[4 * 2];
        Array.Fill(pixels, (byte)255);
        Blit(
            buffer,
            1,
            1,
            4,
            2,
            4,
            pixels,
            PixelModeGray
        );
        CountDark(buffer).ShouldBe(8);
    }

    [Fact]
    public void GrayPath_NegativePitch_BlitsBottomUp()
    {
        var buffer = new RasterBuffer(8, 4);
        buffer.Clear();
        // Negative pitch signals bottom-up row order; both rows are fully covered.
        var pixels = new byte[4 * 2];
        Array.Fill(pixels, (byte)255);
        Blit(
            buffer,
            1,
            1,
            4,
            2,
            -4,
            pixels,
            PixelModeGray
        );
        CountDark(buffer).ShouldBe(8);
    }

    [Fact]
    public void MonoPath_PositivePitch_UnpacksBitsToCoverage()
    {
        var buffer = new RasterBuffer(8, 4);
        buffer.Clear();
        // 8-pixel-wide mono row 0b10101010 → 4 set bits per row, 2 rows = 8 dark pixels.
        var pixels = new byte[] { 0b10101010, 0b10101010 };
        Blit(
            buffer,
            0,
            1,
            8,
            2,
            1,
            pixels,
            PixelModeMono
        );
        CountDark(buffer).ShouldBe(8);
    }

    [Fact]
    public void MonoPath_NegativePitch_BlitsBottomUp()
    {
        var buffer = new RasterBuffer(8, 4);
        buffer.Clear();
        // Asymmetric data: row 0 = all dark (0xFF), row 1 = all light (0x00).
        // With negative pitch, data row 0 maps to the BOTTOM of the glyph,
        // data row 1 maps to the TOP of the glyph.
        var pixels = new byte[] { 0xFF, 0x00 };
        Blit(
            buffer,
            0,
            1,
            8,
            2,
            -1,
            pixels,
            PixelModeMono
        );
        // data[0]=0xFF → bottom of glyph (buffer row 2), data[1]=0x00 → top of glyph (buffer row 1).
        // All 8 pixels in the glyph area should be dark.
        CountDark(buffer).ShouldBe(8);
    }

    [Fact]
    public void OversizeDimensions_ReturnsWithoutBlitting()
    {
        var buffer = new RasterBuffer(8, 4);
        buffer.Clear();
        // width over 4096 → early-out guard; nothing is painted.
        Blit(
            buffer,
            0,
            0,
            5000,
            2,
            5000,
            [255],
            PixelModeGray
        );
        CountDark(buffer).ShouldBe(0);
    }

    [Fact]
    public void UnknownPixelMode_IsNoOp()
    {
        var buffer = new RasterBuffer(8, 4);
        buffer.Clear();
        var pixels = new byte[4];
        Array.Fill(pixels, (byte)255);
        // Pixel mode 3 (BGRA / LCD) is not handled → switch falls through, nothing painted.
        Blit(
            buffer,
            0,
            0,
            4,
            1,
            4,
            pixels,
            3
        );
        CountDark(buffer).ShouldBe(0);
    }

    [Fact]
    public void ZeroPitch_ReturnsEarly()
    {
        var buffer = new RasterBuffer(8, 4);
        buffer.Clear();
        Blit(
            buffer,
            0,
            0,
            4,
            1,
            0,
            [255, 255, 255, 255],
            PixelModeGray
        );
        CountDark(buffer).ShouldBe(0);
    }

    [Fact]
    public void NullBuffer_ReturnsEarly()
    {
        var buffer = new RasterBuffer(8, 4);
        buffer.Clear();
        Blit(
            buffer,
            0,
            0,
            4,
            1,
            4,
            null,
            PixelModeGray
        );
        CountDark(buffer).ShouldBe(0);
    }
}
