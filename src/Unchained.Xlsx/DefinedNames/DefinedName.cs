namespace Unchained.Xlsx.DefinedNames;

/// <summary>
///     A defined name (named range): a workbook- or sheet-scoped name that resolves to a formula,
///     typically a cell range such as <c>Sheet1!$A$1:$C$10</c>.
/// </summary>
public sealed class DefinedName
{
    internal DefinedName(string name, string formula, int? localSheetId)
    {
        Name = name;
        Formula = formula;
        LocalSheetId = localSheetId;
    }

    /// <summary>The name as referenced in formulas.</summary>
    public string Name { get; set; }

    /// <summary>The formula the name resolves to (e.g. <c>Sheet1!$A$1:$C$10</c>).</summary>
    public string Formula { get; set; }

    /// <summary>An optional comment describing the name.</summary>
    public string? Comment { get; set; }

    /// <summary>
    ///     The zero-based local sheet index when the name is sheet-scoped, or <see langword="null" />
    ///     when it is workbook-scoped.
    /// </summary>
    public int? LocalSheetId { get; internal set; }

    /// <summary><see langword="true" /> when this name is visible across the whole workbook.</summary>
    public bool IsWorkbookScoped => LocalSheetId is null;

    /// <summary>
    ///     <see langword="true" /> for the reserved built-in names (<c>Print_Area</c>,
    ///     <c>Print_Titles</c>, <c>_FilterDatabase</c>, …).
    /// </summary>
    public bool IsBuiltIn => Name.StartsWith("_xlnm.", StringComparison.Ordinal);

    /// <summary><see langword="true" /> when the name should be hidden in the application UI.</summary>
    public bool IsHidden { get; set; }
}
