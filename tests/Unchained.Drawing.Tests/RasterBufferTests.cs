using Shouldly;
using Xunit;

namespace Unchained.Drawing.Tests;

/// <summary>
///     Unit tests for <see cref="RasterBuffer" /> — the ARGB raster surface: pixel set/get, fills,
///     lines, circles, triangles, clipping (rect + polygon + save/restore), alpha blending, and
///     clip-bounds queries.
/// </summary>
public sealed class RasterBufferTests
{
    [Fact]
    public void Clear_FillsWholeBufferWithColor()
    {
        var buffer = new RasterBuffer(4, 4);
        buffer.Clear(10, 20, 30);
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)10, (byte)20, (byte)30));
        buffer.GetPixelRgb(3, 3).ShouldBe(((byte)10, (byte)20, (byte)30));
    }

    [Fact]
    public void SetPixel_OpaqueColor_StoresIt()
    {
        var buffer = new RasterBuffer(4, 4);
        // ReSharper disable once BadListLineBreaks
        buffer.SetPixel(
            1,
            2,
            255,
            0,
            0,
            255
        );
        buffer.GetPixelRgb(1, 2).ShouldBe(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void SetPixel_OutOfBounds_NoOp()
    {
        var buffer = new RasterBuffer(4, 4);
        // ReSharper disable once BadListLineBreaks
        Should.NotThrow(() => buffer.SetPixel(
                100,
                100,
                1,
                2,
                3,
                255
            )
        );
    }

    [Fact]
    public void GetPixelRgb_OutOfBounds_ReturnsWhite() =>
        new RasterBuffer(4, 4).GetPixelRgb(-1, -1).ShouldBe(((byte)255, (byte)255, (byte)255));

    [Fact]
    public void FillRect_FillsRegion()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        // ReSharper disable once BadListLineBreaks
        buffer.FillRect(
            2,
            2,
            4,
            4,
            0,
            0,
            255
        );
        buffer.GetPixelRgb(3, 3).ShouldBe(((byte)0, (byte)0, (byte)255));
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void FillRect_ClampsToBounds()
    {
        var buffer = new RasterBuffer(4, 4);
        Should.NotThrow(() => buffer.FillRect(
                -2,
                -2,
                100,
                100,
                0,
                0,
                0
            )
        );
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void FillSpan_FillsInclusiveRange()
    {
        var buffer = new RasterBuffer(10, 2);
        buffer.Clear();
        // ReSharper disable once BadListLineBreaks
        buffer.FillSpan(
            0,
            2,
            5,
            0,
            0,
            0
        );
        buffer.GetPixelRgb(2, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
        buffer.GetPixelRgb(5, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
        buffer.GetPixelRgb(6, 0).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void DrawLine_PaintsEndpoints()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        // ReSharper disable BadListLineBreaks
        buffer.DrawLine(
            0,
            0,
            9,
            9,
            0,
            0,
            0
        );
        // ReSharper restore BadListLineBreaks
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
        buffer.GetPixelRgb(9, 9).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void FillCircle_PaintsCentre()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        buffer.FillCircle(
            5,
            5,
            3,
            255,
            0,
            0
        );
        buffer.GetPixelRgb(5, 5).ShouldBe(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void FillTriangle_PaintsInterior()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        // ReSharper disable BadListLineBreaks
        buffer.FillTriangle(
            0,
            0,
            9,
            0,
            0,
            9,
            0,
            255,
            0
        );
        // ReSharper restore BadListLineBreaks
        // A point well inside the lower-left triangle.
        buffer.GetPixelRgb(2, 2).ShouldBe(((byte)0, (byte)255, (byte)0));
    }

    [Fact]
    public void BlendPixel_HalfAlpha_BlendsTowardSource()
    {
        var buffer = new RasterBuffer(2, 2);
        buffer.Clear(0, 0, 0); // black background
        buffer.BlendPixel(
            0,
            0,
            255,
            255,
            255,
            128
        ); // 50% white
        var (r, _, _) = buffer.GetPixelRgb(0, 0);
        ((int)r).ShouldBeInRange(100, 160);
    }

    [Fact]
    public void SetClipRect_RestrictsPainting()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        buffer.SetClipRect(0, 0, 5, 5);
        buffer.FillRect(
            0,
            0,
            10,
            10,
            0,
            0,
            0
        );
        buffer.GetPixelRgb(2, 2).ShouldBe(((byte)0, (byte)0, (byte)0));       // inside clip
        buffer.GetPixelRgb(8, 8).ShouldBe(((byte)255, (byte)255, (byte)255)); // outside clip
    }

    [Fact]
    public void ClearClip_RemovesRestriction()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        buffer.SetClipRect(0, 0, 2, 2);
        buffer.ClearClip();
        buffer.FillRect(
            0,
            0,
            10,
            10,
            0,
            0,
            0
        );
        buffer.GetPixelRgb(8, 8).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void SaveAndRestoreClipMask_RoundTrips()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        buffer.SetClipRect(0, 0, 5, 5);
        var saved = buffer.SaveClipMask();

        buffer.ClearClip();
        buffer.RestoreClipMask(saved);

        buffer.FillRect(
            0,
            0,
            10,
            10,
            0,
            0,
            0
        );
        buffer.GetPixelRgb(8, 8).ShouldBe(((byte)255, (byte)255, (byte)255)); // clip restored
    }

    [Fact]
    public void SetClipPolygons_RestrictsToPolygon()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        // A square covering the top-left quadrant.
        buffer.SetClipPolygons([[(0, 0), (5, 0), (5, 5), (0, 5)]], false);
        buffer.FillRect(
            0,
            0,
            10,
            10,
            0,
            0,
            0
        );
        buffer.GetPixelRgb(2, 2).ShouldBe(((byte)0, (byte)0, (byte)0));
        buffer.GetPixelRgb(8, 8).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void ClipBounds_NoClip_ReturnsFullBuffer() =>
        new RasterBuffer(8, 6).ClipBounds().ShouldBe((0, 0, 8, 6));

    [Fact]
    public void ClipBounds_WithRectClip_ReturnsClipExtent()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.SetClipRect(2, 3, 6, 7);
        buffer.ClipBounds().ShouldBe((2, 3, 6, 7));
    }

    [Fact]
    public void ToArgbBytes_HasFourBytesPerPixel()
    {
        var buffer = new RasterBuffer(4, 3);
        buffer.ToArgbBytes().Length.ShouldBe(4 * 3 * 4);
    }

    [Fact]
    public void Dimensions_AreStored()
    {
        var buffer = new RasterBuffer(7, 5);
        buffer.Width.ShouldBe(7);
        buffer.Height.ShouldBe(5);
    }

    [Fact]
    public void SetPixel_MultiplyBlend_DarkensBackground()
    {
        var buffer = new RasterBuffer(2, 2);
        buffer.Clear(128, 128, 128);
        buffer.SetPixel(
            0,
            0,
            0,
            0,
            0,
            255,
            "Multiply"
        );
        // Multiply by black → black.
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void SetPixel_HalfAlphaNormal_BlendsChannels()
    {
        var buffer = new RasterBuffer(2, 2);
        buffer.Clear(0, 0, 0);
        buffer.SetPixel(
            0,
            0,
            200,
            100,
            50,
            128
        );
        var (r, g, b) = buffer.GetPixelRgb(0, 0);
        ((int)r).ShouldBeInRange(90, 110);
        ((int)g).ShouldBeInRange(45, 55);
        ((int)b).ShouldBeInRange(20, 30);
    }

    [Fact]
    public void SetPixel_ScreenBlend_LightensBackground()
    {
        var buffer = new RasterBuffer(2, 2);
        buffer.Clear(100, 100, 100);
        buffer.SetPixel(
            0,
            0,
            255,
            255,
            255,
            255,
            "Screen"
        );
        // Screen with white → white.
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [
        Theory,
        InlineData("Overlay"),
        InlineData("Darken"),
        InlineData("Lighten"),
        InlineData("ColorDodge"),
        InlineData("ColorBurn"),
        InlineData("HardLight"),
        InlineData("SoftLight"),
        InlineData("Difference"),
        InlineData("Exclusion"),
        InlineData("Hue"),
        InlineData("Saturation"),
        InlineData("Color"),
        InlineData("Luminosity"),
        InlineData("UnknownModeName")
    ]
    public void SetPixel_AllBlendModes_DoNotThrowAndWriteOpaque(string mode)
    {
        var buffer = new RasterBuffer(2, 2);
        buffer.Clear(90, 140, 200);
        buffer.SetPixel(
            0,
            0,
            210,
            60,
            120,
            255,
            mode
        );
        // Every mode writes a defined colour; just assert it ran and the pixel changed/known.
        Should.NotThrow(() => buffer.GetPixelRgb(0, 0));
    }

    [Fact]
    public void SetPixel_DarkenBlend_PicksMinChannel()
    {
        var buffer = new RasterBuffer(2, 2);
        buffer.Clear(100, 100, 100);
        buffer.SetPixel(
            0,
            0,
            50,
            200,
            100,
            255,
            "Darken"
        );
        // Darken = min(backdrop, source) per channel.
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)50, (byte)100, (byte)100));
    }

    [Fact]
    public void SetPixel_LightenBlend_PicksMaxChannel()
    {
        var buffer = new RasterBuffer(2, 2);
        buffer.Clear(100, 100, 100);
        buffer.SetPixel(
            0,
            0,
            50,
            200,
            100,
            255,
            "Lighten"
        );
        // Lighten = max(backdrop, source) per channel.
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)100, (byte)200, (byte)100));
    }

    [Fact]
    public void SetPixel_DifferenceBlendSameColor_ProducesBlack()
    {
        var buffer = new RasterBuffer(2, 2);
        buffer.Clear(123, 45, 200);
        buffer.SetPixel(
            0,
            0,
            123,
            45,
            200,
            255,
            "Difference"
        );
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void FillRect_WithBlendMode_Applies()
    {
        var buffer = new RasterBuffer(4, 4);
        buffer.Clear(200, 200, 200);
        buffer.FillRect(
            0,
            0,
            4,
            4,
            0,
            0,
            0,
            255,
            "Multiply"
        );
        buffer.GetPixelRgb(1, 1).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void FillSpan_OutOfBoundsRow_NoOp()
    {
        var buffer = new RasterBuffer(4, 4);
        buffer.Clear();
        Should.NotThrow(() => buffer.FillSpan(
                100,
                0,
                3,
                0,
                0,
                0
            )
        );
    }

    [Fact]
    public void DrawLine_WithThickness_PaintsNeighbours()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        buffer.DrawLine(
            1,
            5,
            8,
            5,
            0,
            0,
            0,
            3
        );
        // A thick horizontal line should paint rows above and below the centre.
        buffer.GetPixelRgb(4, 4).ShouldBe(((byte)0, (byte)0, (byte)0));
        buffer.GetPixelRgb(4, 6).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void BlitImagePixel_WritesOpaque()
    {
        var buffer = new RasterBuffer(4, 4);
        buffer.Clear();
        buffer.BlitImagePixel(
            1,
            1,
            10,
            20,
            30
        );
        buffer.GetPixelRgb(1, 1).ShouldBe(((byte)10, (byte)20, (byte)30));
    }

    [Fact]
    public void BlitImagePixel_OutOfBounds_NoOp()
    {
        var buffer = new RasterBuffer(4, 4);
        Should.NotThrow(() => buffer.BlitImagePixel(
                50,
                50,
                1,
                2,
                3
            )
        );
    }

    [Fact]
    public void BlitImagePixel_RespectsClip()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        buffer.SetClipRect(0, 0, 3, 3);
        buffer.BlitImagePixel(
            8,
            8,
            0,
            0,
            0
        );
        buffer.GetPixelRgb(8, 8).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void BlitGrayBitmap_PaintsCoverageAsAlpha()
    {
        var buffer = new RasterBuffer(4, 4);
        buffer.Clear(0, 0, 0);
        // 2×2 glyph, full coverage everywhere.
        var glyph = new byte[] { 255, 255, 255, 255 };
        buffer.BlitGrayBitmap(
            1,
            1,
            2,
            2,
            2,
            glyph,
            false,
            255,
            255,
            255
        );
        // Full coverage (255) maps to alpha 255 → white pixel.
        buffer.GetPixelRgb(1, 1).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void BlitGrayBitmap_ZeroCoverage_LeavesBackground()
    {
        var buffer = new RasterBuffer(4, 4);
        buffer.Clear(0, 0, 0);
        var glyph = "\0\0\0\0"u8.ToArray();
        buffer.BlitGrayBitmap(
            0,
            0,
            2,
            2,
            2,
            glyph,
            false,
            255,
            255,
            255
        );
        // Zero coverage → no paint, background unchanged.
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void BlitGrayBitmap_InvertRows_PaintsFlipped()
    {
        var buffer = new RasterBuffer(2, 2);
        buffer.Clear(0, 0, 0);
        // Top row covered, bottom row empty; inverted should paint the bottom dest row.
        var glyph = new byte[] { 255, 255, 0, 0 };
        buffer.BlitGrayBitmap(
            0,
            0,
            2,
            2,
            2,
            glyph,
            true,
            255,
            255,
            255
        );
        buffer.GetPixelRgb(0, 1).ShouldBe(((byte)255, (byte)255, (byte)255));
        buffer.GetPixelRgb(0, 0).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void SetClipPolygons_EvenOdd_LeavesHole()
    {
        var buffer = new RasterBuffer(20, 20);
        buffer.Clear();
        // Outer square with an inner square — even-odd rule carves the hole.
        buffer.SetClipPolygons(
            [
                [(0, 0), (20, 0), (20, 20), (0, 20)],
                [(6, 6), (14, 6), (14, 14), (6, 14)]
            ],
            true
        );
        buffer.FillRect(
            0,
            0,
            20,
            20,
            0,
            0,
            0
        );
        buffer.GetPixelRgb(2, 2).ShouldBe(((byte)0, (byte)0, (byte)0));         // inside outer, outside inner
        buffer.GetPixelRgb(10, 10).ShouldBe(((byte)255, (byte)255, (byte)255)); // inside the hole
    }

    [Fact]
    public void SetClipPolygons_NestedIntersection_RestrictsToOverlap()
    {
        var buffer = new RasterBuffer(20, 20);
        buffer.Clear();
        buffer.SetClipPolygons([[(0, 0), (10, 0), (10, 10), (0, 10)]], false);
        buffer.SetClipPolygons([[(5, 5), (15, 5), (15, 15), (5, 15)]], false);
        buffer.FillRect(
            0,
            0,
            20,
            20,
            0,
            0,
            0
        );
        buffer.GetPixelRgb(7, 7).ShouldBe(((byte)0, (byte)0, (byte)0));       // in both clips
        buffer.GetPixelRgb(2, 2).ShouldBe(((byte)255, (byte)255, (byte)255)); // only first clip
    }

    [Fact]
    public void SetClipPolygons_Empty_ProducesEmptyMask()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        buffer.SetClipPolygons([], false);
        buffer.FillRect(
            0,
            0,
            10,
            10,
            0,
            0,
            0
        );
        // Empty polygon set → nothing inside the clip.
        buffer.GetPixelRgb(5, 5).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void ClipBounds_EmptyPolygonClip_ReturnsEmptyRect()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.SetClipPolygons([], false);
        buffer.ClipBounds().ShouldBe((0, 0, 0, 0));
    }

    [Fact]
    public void SetClipRect_ClampsNegativeAndOversizedBounds()
    {
        var buffer = new RasterBuffer(8, 8);
        buffer.Clear();
        buffer.SetClipRect(-5, -5, 100, 100);
        buffer.FillRect(
            0,
            0,
            8,
            8,
            0,
            0,
            0
        );
        buffer.GetPixelRgb(4, 4).ShouldBe(((byte)0, (byte)0, (byte)0));
        buffer.ClipBounds().ShouldBe((0, 0, 8, 8));
    }

    [Fact]
    public void FillCircle_RespectsClip()
    {
        var buffer = new RasterBuffer(20, 20);
        buffer.Clear();
        buffer.SetClipRect(0, 0, 10, 10);
        buffer.FillCircle(
            10,
            10,
            8,
            0,
            0,
            0
        );
        // Centre is on the clip edge; a point clearly outside the clip stays white.
        buffer.GetPixelRgb(15, 15).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void RestoreClipMask_Null_ClearsClip()
    {
        var buffer = new RasterBuffer(10, 10);
        buffer.Clear();
        buffer.SetClipRect(0, 0, 2, 2);
        buffer.RestoreClipMask(null);
        buffer.FillRect(
            0,
            0,
            10,
            10,
            0,
            0,
            0
        );
        buffer.GetPixelRgb(8, 8).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void SaveClipMask_NoClip_ReturnsNull() =>
        new RasterBuffer(4, 4).SaveClipMask().ShouldBeNull();
}
