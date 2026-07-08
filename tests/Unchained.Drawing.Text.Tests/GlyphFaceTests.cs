using Shouldly;
using Unchained.Drawing.Text.Extensions;
using Xunit;

namespace Unchained.Drawing.Text.Tests;

/// <summary>
///     Unit tests for <see cref="GlyphFace" /> and <see cref="RasterBufferGlyphExtensions" />
///     (both in Unchained.Drawing.Text, reachable via InternalsVisibleTo). Exercises face loading,
///     sizing, char-index lookup, advance measurement, outline extraction, glyph rasterization, and
///     blitting a rendered glyph onto a <see cref="RasterBuffer" />.
/// </summary>
public sealed class GlyphFaceTests : IDisposable
{
    private readonly FontCache _cache = new();

    public void Dispose() => _cache.Dispose();

    private GlyphFace Face() =>
        _cache.GetFace("DejaVuSans", BundledFonts.DejaVuSansRegular());

    [Fact]
    public void UnitsPerEm_IsPositive() => Face().UnitsPerEm.ShouldBeGreaterThan((ushort)0);

    [Fact]
    public void GetCharIndex_KnownGlyph_ReturnsNonZero() =>
        // 'A' (U+0041) exists in DejaVu Sans → non-zero glyph index.
        Face().GetCharIndex('A').ShouldBeGreaterThan(0u);

    [Fact]
    public void GetCharIndex_UnsupportedCodepoint_ReturnsZero() =>
        // A Private-Use-Area codepoint is unlikely to be mapped → index 0 (.notdef).
        Face().GetCharIndex(0xE123).ShouldBe(0u);

    [Fact]
    public void TryLoadGlyph_ValidGlyph_ReturnsTrueAndBitmap()
    {
        var face = Face();
        face.SetPixelSize(48);
        var gid = face.GetCharIndex('H');
        face.TryLoadGlyph(gid).ShouldBeTrue();

        var bm = face.GetGlyphBitmap();
        bm.Width.ShouldBeGreaterThan(0);
        bm.Rows.ShouldBeGreaterThan(0);
        bm.Buffer.ShouldNotBe(IntPtr.Zero);
    }

    [Fact]
    public void TryLoadGlyph_WithHinting_Succeeds()
    {
        var face = Face();
        face.SetPixelSize(24);
        var gid = face.GetCharIndex('g');
        face.TryLoadGlyph(gid, true).ShouldBeTrue();
    }

    [Fact]
    public void GetAdvance_KnownGlyph_IsPositive()
    {
        var face = Face();
        face.SetPixelSize(32);
        var gid = face.GetCharIndex('M');
        face.GetAdvance(gid).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void TryLoadGlyphOutline_AndContours_AreReturned()
    {
        var face = Face();
        face.SetPixelSize(64);
        var gid = face.GetCharIndex('O');
        face.TryLoadGlyphOutline(gid).ShouldBeTrue();

        var contours = face.GetGlyphContours();
        // 'O' has an outer and inner contour.
        contours.Count.ShouldBeGreaterThanOrEqualTo(1);
        contours[0].Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void SpaceGlyph_HasNoContours()
    {
        var face = Face();
        face.SetPixelSize(32);
        var gid = face.GetCharIndex(' ');
        face.TryLoadGlyphOutline(gid);
        // A space has an empty outline.
        face.GetGlyphContours().ShouldBeEmpty();
    }

    // ── RasterBufferGlyphExtensions ───────────────────────────────────────────

    [Fact]
    public void BlitGlyphFromFace_RendersDarkPixels()
    {
        var face = Face();
        face.SetPixelSize(48);
        var gid = face.GetCharIndex('H');
        face.TryLoadGlyph(gid).ShouldBeTrue();

        var buffer = new RasterBuffer(80, 80);
        buffer.Clear();
        // Pen near baseline so the glyph lands inside the buffer.
        buffer.BlitGlyphFromFace(
            20,
            60,
            face,
            0,
            0,
            0
        );

        var dark = 0;
        for (var y = 0; y < 80; y++)
        for (var x = 0; x < 80; x++)
        {
            var (r, g, b) = buffer.GetPixelRgb(x, y);
            if (r < 128 && g < 128 && b < 128) dark++;
        }

        dark.ShouldBeGreaterThan(10, "blitting 'H' at 48px should produce visible dark pixels");
    }

    [Fact]
    public void BlitGlyphFromFace_ColoredGlyph_UsesGivenColor()
    {
        var face = Face();
        face.SetPixelSize(48);
        var gid = face.GetCharIndex('I');
        face.TryLoadGlyph(gid).ShouldBeTrue();

        var buffer = new RasterBuffer(80, 80);
        buffer.Clear();
        buffer.BlitGlyphFromFace(
            30,
            60,
            face,
            255,
            0,
            0
        );

        var redFound = false;
        for (var y = 0; y < 80 && !redFound; y++)
        for (var x = 0; x < 80; x++)
        {
            var (r, g, b) = buffer.GetPixelRgb(x, y);
            // Color dominance: red is dominant and notably brighter than green/blue.
            if (r <= g + 80 || r <= b + 80) continue;

            redFound = true;
            break;
        }

        redFound.ShouldBeTrue("a red glyph should leave reddish pixels");
    }
}
