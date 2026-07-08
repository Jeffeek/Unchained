using Shouldly;
using Unchained.Drawing.Constants;
using Xunit;

namespace Unchained.Drawing.Text.Tests;

/// <summary>
///     Direct unit tests for <see cref="FontCache" /> — substitute-font selection across the Standard
///     14 family, embedded-byte caching, the malformed-font fallback, and the
///     <see cref="FontCache.DiagnoseGlyphRender" /> success/failure summaries. Requires the FreeType2
///     native library; skipped when it is unavailable.
/// </summary>
public sealed class FontCacheTests
{
    [
        Theory,
        InlineData("Helvetica-Bold"),
        InlineData("Helvetica-BoldOblique"),
        InlineData("Arial-Bold"),
        InlineData("Arial-BoldItalic"),
        InlineData("Helvetica-Oblique"),
        InlineData("Arial-Italic"),
        InlineData("Helvetica"),
        InlineData("Arial"),
        InlineData("Calibri"),
        InlineData(FontFallbackNames.TimesBold),
        InlineData("Times-BoldItalic"),
        InlineData("Times-Roman"),
        InlineData("Times-Italic"),
        InlineData("Courier"),
        InlineData("Courier-Bold"),
        InlineData("Courier-Oblique"),
        InlineData("Courier-BoldOblique"),
        InlineData("SomeUnknownFont")
    ]
    public void GetFace_SubstituteFonts_ResolveWithoutThrowing(string fontName)
    {
        using var cache = new FontCache();
        var face = cache.GetFace(fontName);
        face.ShouldNotBeNull();
    }

    [Fact]
    public void GetFonts_SameKey_ReturnsCachedInstance()
    {
        using var cache = new FontCache();
        var first = cache.GetFonts("Helvetica");
        var second = cache.GetFonts("Helvetica");

        second.Face.ShouldBeSameAs(first.Face);
        second.HbFont.ShouldBeSameAs(first.HbFont);
    }

    [Fact]
    public void GetFonts_EmbeddedBytes_AreUsed()
    {
        var bytes = BundledFonts.DejaVuSansRegular();
        using var cache = new FontCache();
        var (face, _) = cache.GetFonts("Embedded+Font", bytes);
        face.ShouldNotBeNull();
    }

    [Fact]
    public void GetFonts_MalformedEmbeddedBytes_FallsBackToSubstitute()
    {
        // Garbage "font" bytes: CreatePair throws, GetFonts falls back to the substitute font.
        var garbage = new byte[256];
        new Random(7).NextBytes(garbage);

        using var cache = new FontCache();
        var (face, _) = cache.GetFonts("Helvetica", garbage);
        face.ShouldNotBeNull();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var cache = new FontCache();
        cache.GetFace("Helvetica");
        cache.Dispose();
        Should.NotThrow(() => cache.Dispose());
    }

    [Fact]
    public void DiagnoseGlyphRender_KnownGlyph_ReturnsOkSummary()
    {
        using var cache = new FontCache();
        var result = cache.DiagnoseGlyphRender("Helvetica", null, 'A', 24);
        result.ShouldStartWith("OK:");
        result.ShouldContain("glyphId=");
    }

    [Fact]
    public void DiagnoseGlyphRender_ReturnsStringNeverThrows()
    {
        using var cache = new FontCache();
        // A control character may produce a .notdef glyph but must still return a summary string.
        var result = cache.DiagnoseGlyphRender("Courier", null, '\0', 12);
        result.ShouldNotBeNullOrEmpty();
    }
}
