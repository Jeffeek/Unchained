using Unchained.Drawing;
using Unchained.Drawing.Primitives;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Rendering.Engine.Rasterizers;

/// <summary>
///     Renders connector shapes: the connecting line (with flip handling) and any configured
///     head/tail arrowheads (filled triangles). Pure and state-free; extracted from
///     <see cref="SlideRasterizer" />.
/// </summary>
internal static class ConnectorRasterizer
{
    internal static void RenderConnector(
        RasterBuffer buffer,
        Shape shape,
        int x,
        int y,
        int width,
        int height,
        ColorScheme? colorScheme
    )
    {
        var x0 = x;
        var y0 = y;
        var x1 = x + width;
        var y1 = y + height;
        if (shape.FlipHorizontal) (x0, x1) = (x1, x0);
        if (shape.FlipVertical) (y0, y1) = (y1, y0);

        byte r = 0, g = 0, b = 0;
        if (shape.Line.Fill is { Type: FillType.Solid, Solid: not null })
            (_, r, g, b) = ColorMath.UnpackArgb(shape.Line.Fill.Solid.Color.Resolve(colorScheme));

        var thickness = Math.Max(1, (int)Math.Round((shape.Line.WidthPoints ?? 1.0) * 1.333));
        buffer.DrawLine(
            x0,
            y0,
            x1,
            y1,
            r,
            g,
            b,
            thickness
        );

        // Arrow heads — draw at endpoints if configured.
        if (shape.Line.HeadArrow.HeadType != ArrowHeadType.None)
        {
            DrawArrowHead(
                buffer,
                x1,
                y1,
                x0,
                y0,
                shape.Line.HeadArrow.HeadType,
                shape.Line.HeadArrow.Width,
                shape.Line.HeadArrow.Length,
                r,
                g,
                b
            );
        }

        if (shape.Line.TailArrow.HeadType != ArrowHeadType.None)
        {
            DrawArrowHead(
                buffer,
                x0,
                y0,
                x1,
                y1,
                shape.Line.TailArrow.HeadType,
                shape.Line.TailArrow.Width,
                shape.Line.TailArrow.Length,
                r,
                g,
                b
            );
        }
    }

    // Draws a filled arrowhead at tipX/tipY pointing FROM fromX/fromY.
    private static void DrawArrowHead(
        RasterBuffer buffer,
        int fromX,
        int fromY,
        int tipX,
        int tipY,
        ArrowHeadType headType,
        ArrowHeadSize headWidth,
        ArrowHeadSize headLength,
        byte r,
        byte g,
        byte b
    )
    {
        if (headType == ArrowHeadType.None) return;

        var size = headLength switch
        {
            ArrowHeadSize.Small => 6,
            ArrowHeadSize.Large => 14,
            _ => 10
        };
        var halfWidth = headWidth switch
        {
            ArrowHeadSize.Small => 3,
            ArrowHeadSize.Large => 7,
            _ => 5
        };

        var dx = tipX - fromX;
        var dy = tipY - fromY;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < 1) return;

        var ux = dx / len;
        var uy = dy / len;
        var px = -uy;

        // Base of the arrowhead triangle (opposite the tip).
        var bx = tipX - (int)(ux * size);
        var by = tipY - (int)(uy * size);
        var lx = bx + (int)(px * halfWidth);
        var ly = by + (int)(ux * halfWidth);
        var rx2 = bx - (int)(px * halfWidth);
        var ry2 = by - (int)(ux * halfWidth);

        DrawFilledTriangle(
            buffer,
            tipX,
            tipY,
            lx,
            ly,
            rx2,
            ry2,
            r,
            g,
            b
        );
    }

    // Fills a triangle using horizontal scan lines.
    private static void DrawFilledTriangle(
        RasterBuffer buffer,
        int x0,
        int y0,
        int x1,
        int y1,
        int x2,
        int y2,
        byte r,
        byte g,
        byte b
    )
    {
        if (y0 > y1)
        {
            (x0, x1) = (x1, x0);
            (y0, y1) = (y1, y0);
        }

        if (y0 > y2)
        {
            (x0, x2) = (x2, x0);
            (y0, y2) = (y2, y0);
        }

        if (y1 > y2)
        {
            (x1, x2) = (x2, x1);
            (y1, y2) = (y2, y1);
        }

        var totalH = y2 - y0;
        if (totalH == 0)
        {
            buffer.DrawLine(
                x0,
                y0,
                x2,
                y0,
                r,
                g,
                b
            );
            return;
        }

        for (var scanY = y0; scanY <= y2; scanY++)
        {
            var isUpperHalf = scanY < y1;
            var segH = isUpperHalf ? y1 - y0 : y2 - y1;
            var alpha = (double)(scanY - y0) / totalH;
            var beta = segH == 0 ? 1.0 : (double)(scanY - (isUpperHalf ? y0 : y1)) / segH;

            var ax = (int)(x0 + ((x2 - x0) * alpha));
            var bx2 = isUpperHalf
                ? (int)(x0 + ((x1 - x0) * beta))
                : (int)(x1 + ((x2 - x1) * beta));

            if (ax > bx2) (ax, bx2) = (bx2, ax);
            buffer.FillRect(
                ax,
                scanY,
                bx2 - ax + 1,
                1,
                r,
                g,
                b
            );
        }
    }
}
