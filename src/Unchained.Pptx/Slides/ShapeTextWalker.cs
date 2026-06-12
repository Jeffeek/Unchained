using Unchained.Ooxml.Text;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Slides;

/// <summary>
///     Internal helper that enumerates every <see cref="TextFrame" /> reachable from a shape
///     collection, recursing through group shapes and table cells. Used by find/replace and
///     text-extraction features so they share one traversal definition.
/// </summary>
internal static class ShapeTextWalker
{
    /// <summary>Yields every text frame carried by the shapes in <paramref name="shapes" />.</summary>
    public static IEnumerable<TextFrame> EnumerateTextFrames(IEnumerable<Shape> shapes)
    {
        foreach (var shape in shapes)
        {
            switch (shape)
            {
                case AutoShape auto:
                    yield return auto.TextFrame;

                break;

                case PictureShape { Caption: { } caption }:
                    yield return caption;

                break;

                case TableShape table:
                    for (var r = 0; r < table.Grid.RowCount; r++)
                    for (var c = 0; c < table.Grid.ColumnCount; c++)
                        yield return table.Grid[c, r].TextFrame;

                break;

                case GroupShape group:
                    foreach (var frame in EnumerateTextFrames(group.Children))
                        yield return frame;

                break;
            }
        }
    }
}
