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
}
