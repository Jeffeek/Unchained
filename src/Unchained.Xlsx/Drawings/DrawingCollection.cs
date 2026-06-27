using System.Collections;

namespace Unchained.Xlsx.Drawings;

/// <summary>The drawings (pictures and charts) anchored on a worksheet.</summary>
public sealed class DrawingCollection : IReadOnlyList<WorksheetDrawing>
{
    private readonly List<WorksheetDrawing> _drawings = [];

    /// <summary>The pictures on the sheet.</summary>
    public IEnumerable<PictureDrawing> Pictures => _drawings.OfType<PictureDrawing>();

    internal IReadOnlyList<WorksheetDrawing> All => _drawings;

    /// <summary>The number of drawings on the sheet.</summary>
    public int Count => _drawings.Count;

    /// <summary>Returns the drawing at the given index.</summary>
    public WorksheetDrawing this[int index] => _drawings[index];

    /// <inheritdoc />
    public IEnumerator<WorksheetDrawing> GetEnumerator() => _drawings.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Adds a drawing to the sheet.</summary>
    public void Add(WorksheetDrawing drawing)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        _drawings.Add(drawing);
    }

    /// <summary>Removes a drawing from the sheet.</summary>
    public void Remove(WorksheetDrawing drawing) => _drawings.Remove(drawing);
}
