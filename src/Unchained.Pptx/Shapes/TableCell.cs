using Unchained.Pptx.Core;
using Unchained.Pptx.Drawing;
using Unchained.Pptx.Text;

namespace Unchained.Pptx.Shapes;

/// <summary>
/// A single cell within a <see cref="TableShape"/>.
/// Each cell has its own text body and border/fill formatting.
/// </summary>
public sealed class TableCell
{
    /// <summary>The text content of the cell.</summary>
    public TextFrame TextFrame { get; } = new();

    /// <summary>Fill applied to this cell's background.</summary>
    public FillFormat Fill { get; } = new();

    /// <summary>Left border of the cell.</summary>
    public LineFormat LeftBorder { get; } = new();

    /// <summary>Right border of the cell.</summary>
    public LineFormat RightBorder { get; } = new();

    /// <summary>Top border of the cell.</summary>
    public LineFormat TopBorder { get; } = new();

    /// <summary>Bottom border of the cell.</summary>
    public LineFormat BottomBorder { get; } = new();

    /// <summary>Number of columns this cell spans. 1 = no merge.</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>Number of rows this cell spans. 1 = no merge.</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>
    /// <see langword="true"/> when this cell is a continuation of a horizontal merge
    /// (hidden under the spanning cell to its left).
    /// </summary>
    public bool IsHorizontalMergeContinuation { get; set; }

    /// <summary>
    /// <see langword="true"/> when this cell is a continuation of a vertical merge
    /// (hidden under the spanning cell above it).
    /// </summary>
    public bool IsVerticalMergeContinuation { get; set; }
}
