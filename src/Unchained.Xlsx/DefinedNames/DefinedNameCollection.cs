using System.Collections;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.DefinedNames;

/// <summary>The workbook's defined names (named ranges), both workbook- and sheet-scoped.</summary>
public sealed class DefinedNameCollection : IReadOnlyList<DefinedName>
{
    private readonly List<DefinedName> _names = [];

    internal IReadOnlyList<DefinedName> All => _names;

    /// <summary>The number of defined names.</summary>
    public int Count => _names.Count;

    /// <summary>Returns the defined name at the given index.</summary>
    public DefinedName this[int index] => _names[index];

    /// <inheritdoc />
    public IEnumerator<DefinedName> GetEnumerator() => _names.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Adds a workbook-scoped name.</summary>
    public DefinedName Add(string name, string formula, string? comment = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(formula);

        var defined = new DefinedName(name, formula, null) { Comment = comment };
        _names.Add(defined);
        return defined;
    }

    /// <summary>Adds a name scoped to a specific worksheet.</summary>
    public DefinedName AddSheetScoped(string name, string formula, Worksheet sheet)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(formula);
        ArgumentNullException.ThrowIfNull(sheet);

        var defined = new DefinedName(name, formula, sheet.TabIndex);
        _names.Add(defined);
        return defined;
    }

    /// <summary>Removes the given defined name.</summary>
    public void Remove(DefinedName name) => _names.Remove(name);

    /// <summary>Returns the first workbook-scoped name with the given identifier, or <see langword="null" />.</summary>
    public DefinedName? Find(string name) =>
        _names.FirstOrDefault(n => n.IsWorkbookScoped && n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the name scoped to <paramref name="scope" />, or <see langword="null" />.</summary>
    public DefinedName? Find(string name, Worksheet scope) =>
        _names.FirstOrDefault(n => n.LocalSheetId == scope.TabIndex && n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    internal void AddExisting(DefinedName name) => _names.Add(name);
}
