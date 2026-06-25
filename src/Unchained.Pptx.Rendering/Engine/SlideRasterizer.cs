using Unchained.Drawing;
using Unchained.Drawing.Primitives;
using Unchained.Drawing.Text;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Core;
using Unchained.Pptx.Media;
using Unchained.Pptx.Rendering.Engine.Rasterizers;
using Unchained.Pptx.Rendering.Models;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Rendering.Engine;

/// <summary>
///     Rasterizes a single <see cref="Slide" /> into a <see cref="RasterBuffer" />
///     using FreeType2 for glyph rendering and HarfBuzz for text shaping.
/// </summary>
/// <remarks>
///     The shape renderers share the injected parameters and the
///     <see cref="SeriesPalette" />, so they are organised as cohesive <c>partial</c> files
///     rather than independent collaborators:
///     <list type="bullet">
///         <item>
///             <c>SlideRasterizer.cs</c> — pipeline orchestration, shape dispatch, groups, auto-shapes and shared
///             helpers.
///         </item>
///         <item>
///             <c>SlideRasterizer.Effects.cs</c> — fills, gradients, bevels, drop shadows, WordArt warp, pictures and
///             tables.
///         </item>
///         <item><c>SlideRasterizer.Text.cs</c> — text layout, measurement, font resolution and glyph blitting.</item>
///         <item><c>SlideRasterizer.Charts.cs</c> — chart plot/axis/legend rendering.</item>
///         <item><c>SlideRasterizer.SmartArt.cs</c> — SmartArt layout heuristics and renderers.</item>
///     </list>
///     Genuinely state-free clusters live in their own classes: <see cref="ConnectorRasterizer" />
///     and <see cref="SlideImageDecoder" />.
/// </remarks>
internal sealed partial class SlideRasterizer(FontCache fonts, MediaStore? media = null)
{
    // Series palette — 8 saturated colours that cycle across series in a chart.
    private static readonly (byte R, byte G, byte B)[] SeriesPalette =
    [
        (68, 114, 196), (237, 125, 49), (165, 165, 165), (255, 192, 0),
        (91, 155, 213), (112, 173, 71), (38, 68, 120), (158, 72, 14)
    ];

    internal RasterBuffer Rasterize(Slide slide, SlideSize slideSize, RenderOptions options)
    {
        var buffer = new RasterBuffer(options.WidthPx, options.HeightPx);

        // Resolve the colour scheme and font scheme for this slide (slide → layout → master).
        var colorScheme = slide.Master.Theme.Colors;
        var fontScheme = slide.Master.Theme.Fonts;

        // Scale factor: EMU → pixels
        var scaleX = (double)options.WidthPx / slideSize.Width.Value;
        var scaleY = (double)options.HeightPx / slideSize.Height.Value;

        // Paint slide background using the inheritance chain.
        PaintBackground(buffer, slide, colorScheme);

        var root = new Transform(scaleX, scaleY, 0, 0);

        // Build a lookup table of layout placeholder shapes for geometry inheritance.
        var layoutPlaceholders = BuildLayoutPlaceholderMap(slide);

        // Composite inherited backdrop shapes from the master and layout BENEATH the slide's own shapes.
        if (slide.Layout.Master is { } master)
        {
            foreach (var shape in master.Shapes.Where(static shape => !shape.IsPlaceholder))
            {
                RenderShape(
                    buffer,
                    shape,
                    root,
                    options.Dpi,
                    colorScheme,
                    layoutPlaceholders,
                    fontScheme
                );
            }
        }

        if (slide.Layout is { } layout)
        {
            foreach (var shape in layout.Shapes.Where(static shape => !shape.IsPlaceholder))
            {
                RenderShape(
                    buffer,
                    shape,
                    root,
                    options.Dpi,
                    colorScheme,
                    layoutPlaceholders,
                    fontScheme
                );
            }
        }

        // Render each shape in Z-order (insertion order = back-to-front).
        foreach (var shape in slide.Shapes)
        {
            RenderShape(
                buffer,
                shape,
                root,
                options.Dpi,
                colorScheme,
                layoutPlaceholders,
                fontScheme
            );
        }

        return buffer;
    }

    // Builds a map from placeholder index → Shape for the slide's layout (and master as fallback),
    // so zero-size placeholder shapes can inherit their geometry.
    private static Dictionary<int, Shape> BuildLayoutPlaceholderMap(Slide slide)
    {
        var map = new Dictionary<int, Shape>();
        foreach (var s in slide.Layout.Master.Shapes.Where(static s => s.PlaceholderIndex.HasValue))
            map.TryAdd(s.PlaceholderIndex!.Value, s);

        foreach (var s in slide.Layout.Shapes.Where(static s => s.PlaceholderIndex.HasValue))
            map[s.PlaceholderIndex!.Value] = s;

        return map;
    }

    // Resolves the effective background fill by walking slide → layout → master.
    private static FillFormat? ResolveBackground(Slide slide) =>
        slide.Background.Fill.Type != FillType.None
            ? slide.Background.Fill
            : slide.Layout.Background.Fill.Type != FillType.None
                ? slide.Layout.Background.Fill
                : slide.Master.Background.Fill.Type != FillType.None
                    ? slide.Master.Background.Fill
                    : null;

    private static void PaintBackground(RasterBuffer buffer, Slide slide, ColorScheme? colorScheme)
    {
        var fill = ResolveBackground(slide);

        if (fill is null)
        {
            buffer.Clear();
            return;
        }

        switch (fill.Type)
        {
            case FillType.Solid when fill.Solid is not null:
            {
                var argb = fill.Solid.Color.Resolve(colorScheme);
                ExtractArgb(argb, out _, out var r, out var g, out var b);
                buffer.Clear(r, g, b);
                break;
            }
            case FillType.Gradient when fill.Gradient is not null && fill.Gradient.Stops.Count >= 2:
            {
                var first = fill.Gradient.Stops[0].Color.Resolve(colorScheme);
                var last = fill.Gradient.Stops[^1].Color.Resolve(colorScheme);
                ExtractArgb(first, out _, out var r1, out var g1, out var b1);
                ExtractArgb(last, out _, out var r2, out var g2, out var b2);
                var h = buffer.Height;
                for (var row = 0; row < h; row++)
                {
                    var t = (double)row / Math.Max(1, h - 1);
                    var r = (byte)(r1 + ((r2 - r1) * t));
                    var g = (byte)(g1 + ((g2 - g1) * t));
                    var bv = (byte)(b1 + ((b2 - b1) * t));
                    buffer.FillRect(
                        0,
                        row,
                        buffer.Width,
                        1,
                        r,
                        g,
                        bv
                    );
                }

                break;
            }
            case FillType.None:
            case FillType.Pattern:
            case FillType.Picture:
            case FillType.Group:
            default:
                buffer.Clear();
            break;
        }
    }

    private void RenderShape(
        RasterBuffer buffer,
        Shape shape,
        Transform transform,
        double dpi,
        ColorScheme? colorScheme = null,
        Dictionary<int, Shape>? layoutPlaceholders = null,
        FontScheme? fontScheme = null
    )
    {
        // Resolve geometry: if this shape is a zero-size placeholder, inherit from layout.
        if (shape.Width.Value <= 0 || shape.Height.Value <= 0)
        {
            if (shape is GroupShape groupShape)
            {
                RenderGroup(
                    buffer,
                    groupShape,
                    transform,
                    dpi,
                    colorScheme,
                    layoutPlaceholders,
                    fontScheme
                );
                return;
            }

            if (shape.PlaceholderIndex.HasValue &&
                layoutPlaceholders is not null &&
                layoutPlaceholders.TryGetValue(shape.PlaceholderIndex.Value, out var layoutShape) &&
                layoutShape.Width.Value > 0 && layoutShape.Height.Value > 0)
            {
                shape.X = layoutShape.X;
                shape.Y = layoutShape.Y;
                shape.Width = layoutShape.Width;
                shape.Height = layoutShape.Height;
            }
            else
                return;
        }

        var x = transform.PxX(shape.X.Value);
        var y = transform.PxY(shape.Y.Value);
        var width = transform.PxW(shape.Width.Value);
        var height = transform.PxH(shape.Height.Value);

        switch (shape)
        {
            case GroupShape group:
                RenderGroup(
                    buffer,
                    group,
                    transform,
                    dpi,
                    colorScheme,
                    layoutPlaceholders,
                    fontScheme
                );
            break;

            case AutoShape autoShape when width > 0 && height > 0:
                RenderAutoShape(
                    buffer,
                    autoShape,
                    x,
                    y,
                    width,
                    height,
                    dpi,
                    colorScheme,
                    fontScheme
                );
            break;

            case PictureShape pictureShape when width > 0 && height > 0:
                RenderPicture(
                    buffer,
                    pictureShape,
                    x,
                    y,
                    width,
                    height
                );
            break;

            case TableShape table when width > 0 && height > 0:
                RenderTable(
                    buffer,
                    table,
                    x,
                    y,
                    width,
                    height,
                    dpi,
                    colorScheme
                );
            break;

            case ConnectorShape connector:
                ConnectorRasterizer.RenderConnector(
                    buffer,
                    connector,
                    x,
                    y,
                    width,
                    height,
                    colorScheme
                );
            break;

            case ChartShape chart when width > 0 && height > 0:
                RenderChart(
                    buffer,
                    chart,
                    x,
                    y,
                    width,
                    height,
                    dpi
                );
            break;

            case SmartArtShape smartArt when width > 0 && height > 0:
                RenderSmartArt(
                    buffer,
                    smartArt,
                    x,
                    y,
                    width,
                    height,
                    dpi
                );
            break;
        }
    }

    private void RenderGroup(
        RasterBuffer buffer,
        GroupShape group,
        Transform parent,
        double dpi,
        ColorScheme? colorScheme = null,
        Dictionary<int, Shape>? layoutPlaceholders = null,
        FontScheme? fontScheme = null
    )
    {
        var childTransform = parent;

        var chExtW = group.ChildExtentWidth.Value;
        var chExtH = group.ChildExtentHeight.Value;
        if (chExtW > 0 && chExtH > 0 && group.Width.Value > 0 && group.Height.Value > 0)
        {
            var sx = (double)group.Width.Value / chExtW;
            var sy = (double)group.Height.Value / chExtH;
            var groupPxX = parent.PxX(group.X.Value);
            var groupPxY = parent.PxY(group.Y.Value);

            childTransform = new Transform(
                parent.ScaleX * sx,
                parent.ScaleY * sy,
                groupPxX - (parent.ScaleX * sx * group.ChildOffsetX.Value),
                groupPxY - (parent.ScaleY * sy * group.ChildOffsetY.Value)
            );
        }

        foreach (var child in group.Children)
        {
            RenderShape(
                buffer,
                child,
                childTransform,
                dpi,
                colorScheme,
                layoutPlaceholders,
                fontScheme
            );
        }
    }

    private void RenderAutoShape(
        RasterBuffer buffer,
        AutoShape shape,
        int x,
        int y,
        int width,
        int height,
        double dpi,
        ColorScheme? colorScheme,
        FontScheme? fontScheme = null
    )
    {
        // Drop shadow — rendered before the fill so it sits underneath the shape.
        if (shape.Effects.OuterShadow is not null)
        {
            RenderDropShadow(
                buffer,
                shape.Effects.OuterShadow,
                x,
                y,
                width,
                height,
                dpi,
                colorScheme
            );
        }

        PaintFill(
            buffer,
            shape.Fill,
            x,
            y,
            width,
            height,
            colorScheme,
            shape.StyleFillColor
        );

        // 3-D bevel — edge highlights/shadows after fill, before text.
        if (shape.ThreeD is { IsEmpty: false, TopBevel: not null })
        {
            RenderBevel(
                buffer,
                shape.ThreeD,
                x,
                y,
                width,
                height,
                dpi
            );
        }

        // WordArt warp: render text to offscreen buffer then blit with curve displacement.
        if (shape.TextFrame.Format.Warp is not null && width > 0 && height > 0)
        {
            var textBuffer = new RasterBuffer(width, height);
            textBuffer.Clear(0, 0, 0); // transparent black
            RenderTextFrame(
                textBuffer,
                shape.TextFrame,
                0,
                0,
                width,
                height,
                dpi,
                colorScheme,
                shape.StyleTextColor,
                shape.PlaceholderType,
                fontScheme
            );
            BlitWarpedText(
                buffer,
                textBuffer,
                x,
                y,
                width,
                height,
                shape.TextFrame.Format.Warp.Preset
            );
        }
        else
        {
            RenderTextFrame(
                buffer,
                shape.TextFrame,
                x,
                y,
                width,
                height,
                dpi,
                colorScheme,
                shape.StyleTextColor,
                shape.PlaceholderType,
                fontScheme
            );
        }
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static void DrawBorder(
        RasterBuffer buffer,
        int x,
        int y,
        int w,
        int h,
        byte r,
        byte g,
        byte b
    )
    {
        buffer.FillRect(
            x,
            y,
            w,
            1,
            r,
            g,
            b
        );
        buffer.FillRect(
            x,
            y + h - 1,
            w,
            1,
            r,
            g,
            b
        );
        buffer.FillRect(
            x,
            y,
            1,
            h,
            r,
            g,
            b
        );
        buffer.FillRect(
            x + w - 1,
            y,
            1,
            h,
            r,
            g,
            b
        );
    }

    private static void ExtractArgb(
        uint argb,
        out byte a,
        out byte r,
        out byte g,
        out byte b
    ) => (a, r, g, b) = ColorMath.UnpackArgb(argb);

    // Maps a coordinate-space EMU point to device pixels: px = (Scale * emu) + Offset.
    // The slide root uses Scale = px/EMU, Offset = 0; each group composes a child transform
    // onto its parent so nested shapes land in the right place.
    private readonly record struct Transform(double ScaleX,
        double ScaleY,
        double OffsetX,
        double OffsetY
    )
    {
        public int PxX(long emu) => (int)((ScaleX * emu) + OffsetX);
        public int PxY(long emu) => (int)((ScaleY * emu) + OffsetY);
        public int PxW(long emu) => (int)(ScaleX * emu);
        public int PxH(long emu) => (int)(ScaleY * emu);
    }
}
