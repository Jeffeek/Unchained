using Unchained.Drawing;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Rendering.Engine;

// SmartArt rendering: heuristic layout selection from the node tree, plus the linear, cycle,
// hierarchy, matrix and pyramid layout renderers. Uses the shared SeriesPalette + text pipeline.
internal sealed partial class SlideRasterizer
{
    // Selects a layout heuristically from the node tree structure, then renders.
    private void RenderSmartArt(
        RasterBuffer buffer,
        SmartArtShape shape,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var roots = shape.Nodes.Where(static n => !string.IsNullOrWhiteSpace(n.Text) || n.Children.Count > 0).ToList();
        if (roots.Count == 0)
        {
            DrawBorder(buffer,
                x,
                y,
                width,
                height,
                180,
                180,
                180);
            return;
        }

        var hasChildren = roots.Any(static n => n.Children.Count > 0);
        var flatTexts = FlattenSmartArt(roots).Where(static t => !string.IsNullOrWhiteSpace(t)).ToList();

        if (hasChildren)
        {
            RenderSmartArtHierarchy(buffer,
                roots,
                x,
                y,
                width,
                height,
                dpi);
        }
        else
        {
            switch (roots.Count)
            {
                case 4 when width >= height:
                    RenderSmartArtMatrix(buffer,
                        flatTexts,
                        x,
                        y,
                        width,
                        height,
                        dpi);
                break;
                case >= 3 and <= 6 when !hasChildren:
                    RenderSmartArtCycle(buffer,
                        flatTexts,
                        x,
                        y,
                        width,
                        height,
                        dpi);
                break;
                case >= 3 when height > width:
                    RenderSmartArtPyramid(buffer,
                        flatTexts,
                        x,
                        y,
                        width,
                        height,
                        dpi);
                break;
                default:
                    RenderSmartArtLinear(buffer,
                        flatTexts,
                        x,
                        y,
                        width,
                        height,
                        dpi);
                break;
            }
        }
    }

    // Linear list: stacked colored boxes top-to-bottom.
    private void RenderSmartArtLinear(
        RasterBuffer buffer,
        IReadOnlyList<string> nodes,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var boxH = Math.Max(12, Math.Min(48, ((height - 4) / Math.Max(1, nodes.Count)) - 4));
        var cy = y + 2;
        for (var i = 0; i < nodes.Count; i++)
        {
            var color = SeriesPalette[i % SeriesPalette.Length];
            buffer.FillRect(x + 2,
                cy,
                width - 4,
                boxH,
                color.R,
                color.G,
                color.B);
            RenderTextFrameText(buffer,
                nodes[i],
                x + 8,
                cy + 2,
                width - 16,
                10.0,
                dpi,
                255,
                255,
                255);
            cy += boxH + 4;
            if (cy > y + height) break;
        }
    }

    // Cycle: nodes arranged in a circle with colored circles.
    private void RenderSmartArtCycle(
        RasterBuffer buffer,
        IReadOnlyList<string> nodes,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var cx = x + (width / 2);
        var cy2 = y + (height / 2);
        var radius = (Math.Min(width, height) / 2) - 20;
        var nodeR = Math.Max(10, radius / 3);
        for (var i = 0; i < nodes.Count; i++)
        {
            var angle = (2 * Math.PI * i / nodes.Count) - (Math.PI / 2);
            var nx = cx + (int)(radius * Math.Cos(angle));
            var ny = cy2 + (int)(radius * Math.Sin(angle));
            var color = SeriesPalette[i % SeriesPalette.Length];
            // Draw circle by filling a square and cropping with distance check.
            for (var py = ny - nodeR; py <= ny + nodeR; py++)
            for (var px = nx - nodeR; px <= nx + nodeR; px++)
            {
                var dx = px - nx;
                var dy = py - ny;
                if ((dx * dx) + (dy * dy) <= nodeR * nodeR)
                    buffer.BlitImagePixel(px, py, color.R, color.G, color.B);
            }

            RenderTextFrameText(buffer,
                TruncateLabel(nodes[i], 8),
                nx - nodeR,
                ny - 5,
                nodeR * 2,
                8.0,
                dpi,
                255,
                255,
                255);
        }
    }

    // Hierarchy: root node at top, children in a row below.
    private void RenderSmartArtHierarchy(
        RasterBuffer buffer,
        IReadOnlyList<SmartArtNode> roots,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var boxW = Math.Max(40, Math.Min(120, (width / Math.Max(1, roots.Count)) - 8));
        var boxH = Math.Max(20, Math.Min(40, (height / 3) - 8));
        var levelH = boxH + 16;

        var nodeSpacing = Math.Max(boxW + 8, width / Math.Max(1, roots.Count));
        for (var i = 0; i < roots.Count; i++)
            DrawNode(roots[i], x + (i * nodeSpacing) + 4, y + 4, i);

        return;

        void DrawNode(
            SmartArtNode node,
            int nx,
            int ny,
            int colorIdx
        )
        {
            var color = SeriesPalette[colorIdx % SeriesPalette.Length];
            buffer.FillRect(nx,
                ny,
                boxW,
                boxH,
                color.R,
                color.G,
                color.B);
            RenderTextFrameText(buffer,
                TruncateLabel(node.Text, 10),
                nx + 4,
                ny + 4,
                boxW - 8,
                9.0,
                dpi,
                255,
                255,
                255);

            if (node.Children.Count == 0) return;

            var childW = Math.Max(30, (width - 8) / Math.Max(1, node.Children.Count));
            var childY = ny + levelH;
            if (childY > y + height) return;

            for (var ci = 0; ci < node.Children.Count; ci++)
            {
                var childX = x + (ci * childW) + 4;
                // Connect line
                buffer.DrawLine(nx + (boxW / 2),
                    ny + boxH,
                    childX + (childW / 2),
                    childY,
                    180,
                    180,
                    180);
                DrawNode(node.Children[ci], childX, childY, colorIdx + ci + 1);
            }
        }
    }

    // Matrix: 2×2 grid of colored boxes.
    private void RenderSmartArtMatrix(
        RasterBuffer buffer,
        IReadOnlyList<string> nodes,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var cellW = (width - 6) / 2;
        var cellH = (height - 6) / 2;
        for (var i = 0; i < Math.Min(4, nodes.Count); i++)
        {
            var col = i % 2;
            var row = i / 2;
            var cx2 = x + 2 + (col * (cellW + 2));
            var cy3 = y + 2 + (row * (cellH + 2));
            var color = SeriesPalette[i % SeriesPalette.Length];
            buffer.FillRect(cx2,
                cy3,
                cellW,
                cellH,
                color.R,
                color.G,
                color.B);
            RenderTextFrameText(buffer,
                nodes[i],
                cx2 + 4,
                cy3 + (cellH / 2) - 6,
                cellW - 8,
                10.0,
                dpi,
                255,
                255,
                255);
        }
    }

    // Pyramid: stacked trapezoids narrowing to the top.
    private void RenderSmartArtPyramid(
        RasterBuffer buffer,
        IReadOnlyList<string> nodes,
        int x,
        int y,
        int width,
        int height,
        double dpi
    )
    {
        var n = Math.Min(nodes.Count, 6);
        var rowH = height / n;
        for (var i = 0; i < n; i++)
        {
            var row = n - 1 - i; // bottom = wide, top = narrow
            var frac = (double)(row + 1) / n;
            var rowW = (int)(width * frac);
            var rx = x + ((width - rowW) / 2);
            var ry = y + (i * rowH);
            var color = SeriesPalette[i % SeriesPalette.Length];
            buffer.FillRect(rx,
                ry,
                rowW,
                rowH - 2,
                color.R,
                color.G,
                color.B);
            RenderTextFrameText(buffer,
                TruncateLabel(nodes[i], 12),
                rx + 4,
                ry + (rowH / 2) - 5,
                rowW - 8,
                9.0,
                dpi,
                255,
                255,
                255);
        }
    }

    private static IEnumerable<string> FlattenSmartArt(IEnumerable<SmartArtNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node.Text;

            foreach (var child in FlattenSmartArt(node.Children))
                yield return child;
        }
    }
}
