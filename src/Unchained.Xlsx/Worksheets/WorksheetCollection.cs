using System.Collections;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Sheets;

namespace Unchained.Xlsx.Worksheets;

/// <summary>
///     The ordered collection of worksheets in a <see cref="SpreadsheetDocument" />.
///     Collection order is the visible tab order; <see cref="Worksheet.SheetId" /> is stable
///     and independent of position.
/// </summary>
public sealed class WorksheetCollection : IReadOnlyList<Worksheet>
{
    private readonly SpreadsheetDocument _document;
    private readonly List<Worksheet> _sheets = [];

    internal WorksheetCollection(SpreadsheetDocument document) => _document = document;

    /// <summary>Returns the worksheet with the given name.</summary>
    /// <exception cref="KeyNotFoundException">Thrown when no sheet with that name exists.</exception>
    public Worksheet this[string name] =>
        Find(name) ?? throw new KeyNotFoundException($"No worksheet named '{name}' exists.");

    /// <summary>The number of worksheets in the workbook.</summary>
    public int Count => _sheets.Count;

    /// <summary>Returns the worksheet at the given zero-based tab index.</summary>
    public Worksheet this[int index] => _sheets[index];

    /// <inheritdoc />
    public IEnumerator<Worksheet> GetEnumerator() => _sheets.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── Lookup ─────────────────────────────────────────────────────────────────

    /// <summary>Returns the worksheet with the given name, or <see langword="null" /> if absent.</summary>
    public Worksheet? Find(string name) =>
        _sheets.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the worksheet with the given stable sheet id, or <see langword="null" /> if absent.</summary>
    public Worksheet? FindById(int sheetId) =>
        _sheets.FirstOrDefault(s => s.SheetId == sheetId);

    /// <summary>Returns the zero-based tab index of <paramref name="sheet" />, or -1 if not present.</summary>
    public int IndexOf(Worksheet sheet) => _sheets.IndexOf(sheet);

    // ── Mutation ───────────────────────────────────────────────────────────────

    /// <summary>Appends a new empty worksheet with the given name.</summary>
    /// <exception cref="ArgumentException">Thrown when the name is invalid or already in use.</exception>
    public Worksheet Add(string name) => Insert(_sheets.Count, name);

    /// <summary>Inserts a new empty worksheet at <paramref name="index" /> with the given name.</summary>
    /// <exception cref="ArgumentException">Thrown when the name is invalid or already in use.</exception>
    public Worksheet Insert(int index, string name)
    {
        Worksheet.ValidateSheetName(name);
        if (Find(name) != null)
            throw new ArgumentException($"A worksheet named '{name}' already exists.", nameof(name));

        var sheet = new Worksheet(
            _document,
            name,
            NextSheetId(),
            string.Empty,
            string.Empty,
            SheetState.Visible
        );
        _sheets.Insert(index, sheet);
        return sheet;
    }

    /// <summary>Removes <paramref name="sheet" /> from the workbook.</summary>
    /// <exception cref="InvalidOperationException">Thrown when removing the last remaining sheet.</exception>
    public void Remove(Worksheet sheet)
    {
        if (_sheets.Count <= 1)
            throw new InvalidOperationException("A workbook must contain at least one worksheet.");

        _sheets.Remove(sheet);
    }

    /// <summary>Removes the worksheet at the given tab index.</summary>
    public void RemoveAt(int index) => Remove(_sheets[index]);

    /// <summary>Moves <paramref name="sheet" /> to a new tab position.</summary>
    public void MoveTo(Worksheet sheet, int newIndex)
    {
        var current = _sheets.IndexOf(sheet);
        if (current < 0)
            throw new ArgumentException("The worksheet does not belong to this workbook.", nameof(sheet));

        _sheets.RemoveAt(current);
        _sheets.Insert(Math.Clamp(newIndex, 0, _sheets.Count), sheet);
    }

    // ── Internal ───────────────────────────────────────────────────────────────

    internal void AddExisting(Worksheet sheet) => _sheets.Add(sheet);

    private int NextSheetId() =>
        _sheets.Count == 0 ? 1 : _sheets.Max(static s => s.SheetId) + 1;
}
