using System.Collections;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Models.Shapes;

namespace Unchained.Pptx.Shapes;

/// <summary>
///     An ordered, mutable collection of <see cref="Shape" /> objects on a slide, master,
///     layout, or group. Implements <see cref="IReadOnlyList{T}" /> for enumeration and
///     provides named factory methods for adding new shapes.
/// </summary>
public sealed class ShapeCollection : IReadOnlyList<Shape>
{
    private readonly List<Shape> _shapes = [];
    private uint _nextShapeId = 2; // 1 is reserved for the group shape root (nvGrpSpPr)

    /// <summary>Returns the highest shape ID currently in use (used by the writer).</summary>
    internal uint MaxShapeId => _shapes.Count > 0 ? _shapes.Max(static s => s.ShapeId) : 1;

    // ── IReadOnlyList<Shape> ─────────────────────────────────────────────────

    /// <inheritdoc />
    public int Count => _shapes.Count;

    /// <inheritdoc cref="IReadOnlyList{T}.this" />
    public Shape this[int index] => _shapes[index];

    /// <inheritdoc />
    public IEnumerator<Shape> GetEnumerator() => _shapes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        _shapes.GetEnumerator();

    // ── Factory methods ──────────────────────────────────────────────────────

    /// <summary>
    ///     Adds a new <see cref="AutoShape" /> of the given type at the specified position
    ///     and returns the new shape.
    /// </summary>
    /// <param name="type">Preset shape geometry type.</param>
    /// <param name="x">Horizontal position of the top-left corner.</param>
    /// <param name="y">Vertical position of the top-left corner.</param>
    /// <param name="width">Width of the bounding box.</param>
    /// <param name="height">Height of the bounding box.</param>
    public AutoShape AddShape(
        AutoShapeType type,
        Emu x,
        Emu y,
        Emu width,
        Emu height
    )
    {
        var shape = new AutoShape
        {
            ShapeType = type,
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
        Enroll(shape);
        return shape;
    }

    /// <summary>
    ///     Adds a new text box at the specified position and returns the new shape.
    /// </summary>
    /// <param name="x">Horizontal position of the top-left corner.</param>
    /// <param name="y">Vertical position of the top-left corner.</param>
    /// <param name="width">Width of the text box.</param>
    /// <param name="height">Height of the text box.</param>
    /// <param name="initialText">Optional initial text content.</param>
    public AutoShape AddTextBox(
        Emu x,
        Emu y,
        Emu width,
        Emu height,
        string? initialText = null
    )
    {
        var shape = new AutoShape
        {
            ShapeType = AutoShapeType.Rectangle,
            IsTextBox = true,
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

        if (initialText != null)
            shape.TextFrame.PlainText = initialText;

        shape.Fill.SetNone();
        shape.Line.SetNone();
        Enroll(shape);
        return shape;
    }

    /// <summary>
    ///     Adds a new <see cref="PictureShape" /> using the supplied image and returns the new shape.
    /// </summary>
    /// <param name="image">The embedded image to display.</param>
    /// <param name="x">Horizontal position of the top-left corner.</param>
    /// <param name="y">Vertical position of the top-left corner.</param>
    /// <param name="width">Width of the picture frame.</param>
    /// <param name="height">Height of the picture frame.</param>
    public PictureShape AddPicture(
        EmbeddedImage image,
        Emu x,
        Emu y,
        Emu width,
        Emu height
    )
    {
        ArgumentNullException.ThrowIfNull(image);
        var shape = new PictureShape
        {
            Image = image,
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
        Enroll(shape);
        return shape;
    }

    /// <summary>
    ///     Adds a new <see cref="TableShape" /> with the given column widths and row heights
    ///     and returns the new shape.
    /// </summary>
    /// <param name="x">Horizontal position of the top-left corner.</param>
    /// <param name="y">Vertical position of the top-left corner.</param>
    /// <param name="columnWidths">Width of each column, left to right.</param>
    /// <param name="rowHeights">Height of each row, top to bottom.</param>
    public TableShape AddTable(
        Emu x,
        Emu y,
        Emu[] columnWidths,
        Emu[] rowHeights
    )
    {
        ArgumentNullException.ThrowIfNull(columnWidths);
        ArgumentNullException.ThrowIfNull(rowHeights);
        if (columnWidths.Length == 0) throw new ArgumentException("At least one column is required.", nameof(columnWidths));
        if (rowHeights.Length == 0) throw new ArgumentException("At least one row is required.", nameof(rowHeights));

        var totalWidth = columnWidths.Aggregate(Emu.Zero, static (a, b) => a + b);
        var totalHeight = rowHeights.Aggregate(Emu.Zero, static (a, b) => a + b);

        var shape = new TableShape
        {
            X = x,
            Y = y,
            Width = totalWidth,
            Height = totalHeight,
            Grid = TableGrid.Create(columnWidths, rowHeights)
        };
        shape.Fill.SetNone();
        Enroll(shape);
        return shape;
    }

    /// <summary>
    ///     Adds a new <see cref="ConnectorShape" /> at the specified position and returns the new shape.
    /// </summary>
    /// <param name="type">The routing style of the connector.</param>
    /// <param name="x">Horizontal position of the start point.</param>
    /// <param name="y">Vertical position of the start point.</param>
    /// <param name="width">Horizontal span to the end point.</param>
    /// <param name="height">Vertical span to the end point.</param>
    public ConnectorShape AddConnector(
        ConnectorType type,
        Emu x,
        Emu y,
        Emu width,
        Emu height
    )
    {
        var shape = new ConnectorShape
        {
            ConnectorType = type,
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
        Enroll(shape);
        return shape;
    }

    /// <summary>
    ///     Adds a straight line connector between two points and returns it. Convenience over
    ///     <see cref="AddConnector" /> using point coordinates instead of position+size.
    /// </summary>
    /// <param name="startX">Horizontal position of the start point.</param>
    /// <param name="startY">Vertical position of the start point.</param>
    /// <param name="endX">Horizontal position of the end point.</param>
    /// <param name="endY">Vertical position of the end point.</param>
    public ConnectorShape AddLine(
        Emu startX,
        Emu startY,
        Emu endX,
        Emu endY
    )
    {
        // OOXML connectors store an x/y/cx/cy bounding box; flipH/flipV encode direction when the
        // end point is left of / above the start. Normalise to a positive-extent box + flips.
        var x = startX.Value <= endX.Value ? startX : endX;
        var y = startY.Value <= endY.Value ? startY : endY;
        var width = new Emu(Math.Abs(endX.Value - startX.Value));
        var height = new Emu(Math.Abs(endY.Value - startY.Value));

        var shape = new ConnectorShape
        {
            ConnectorType = ConnectorType.Straight,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            FlipHorizontal = endX.Value < startX.Value,
            FlipVertical = endY.Value < startY.Value
        };
        Enroll(shape);
        return shape;
    }

    /// <summary>Adds a chart frame of the given type at the specified position and size, and returns it.</summary>
    /// <param name="type">The visual type of the chart.</param>
    /// <param name="x">Horizontal position of the top-left corner.</param>
    /// <param name="y">Vertical position of the top-left corner.</param>
    /// <param name="width">Width of the chart frame.</param>
    /// <param name="height">Height of the chart frame.</param>
    public ChartShape AddChart(
        ChartType type,
        Emu x,
        Emu y,
        Emu width,
        Emu height
    )
    {
        var shape = new ChartShape
        {
            Chart = new ChartModel { Type = type },
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
        Enroll(shape);
        return shape;
    }

    /// <summary>
    ///     Adds an empty <see cref="GroupShape" /> and returns it. Add child shapes via
    ///     <see cref="GroupShape.Children" />.
    /// </summary>
    public GroupShape AddGroup()
    {
        var shape = new GroupShape();
        Enroll(shape);
        return shape;
    }

    // ── Management ───────────────────────────────────────────────────────────

    /// <summary>Removes the given shape from the collection.</summary>
    /// <exception cref="ArgumentException">Thrown when the shape is not in this collection.</exception>
    public void Remove(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (!_shapes.Remove(shape))
            throw new ArgumentException("The shape does not belong to this collection.", nameof(shape));
    }

    /// <summary>Removes the shape at the given zero-based index.</summary>
    public void RemoveAt(int index) => _shapes.RemoveAt(index);

    /// <summary>Moves the given shape to the front of the Z-order (drawn last, appears on top).</summary>
    public void BringToFront(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var index = _shapes.IndexOf(shape);
        if (index < 0) throw new ArgumentException("The shape does not belong to this collection.", nameof(shape));

        if (index == _shapes.Count - 1) return;

        _shapes.RemoveAt(index);
        _shapes.Add(shape);
    }

    /// <summary>Moves the given shape to the back of the Z-order (drawn first, appears behind everything).</summary>
    public void SendToBack(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var index = _shapes.IndexOf(shape);
        switch (index)
        {
            case < 0:
                throw new ArgumentException("The shape does not belong to this collection.", nameof(shape));
            case 0:
                return;
            default:
                _shapes.RemoveAt(index);
                _shapes.Insert(0, shape);
            break;
        }
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    /// <summary>Assigns a shape ID and appends the shape. Called by all factory methods.</summary>
    private void Enroll(Shape shape)
    {
        shape.ShapeId = _nextShapeId++;
        _shapes.Add(shape);
    }

    /// <summary>Adds a shape that already has its ID set (used by the parser).</summary>
    internal void AddParsed(Shape shape)
    {
        if (shape.ShapeId >= _nextShapeId)
            _nextShapeId = shape.ShapeId + 1;
        _shapes.Add(shape);
    }
}
