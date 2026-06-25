using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Pivot;

namespace Unchained.Xlsx.Pivot;

/// <summary>
///     A pivot table over a worksheet source range. Holds the field list, the row/column/page/data
///     placements, and the cached source records. Layout (which fields go where) and the cache
///     (a snapshot of the source data) are both persisted; <see cref="Refresh" /> rebuilds the cache
///     from the current source data. Unchained does not compute the displayed pivot grid — it
///     produces a valid definition + cache that a spreadsheet application renders.
/// </summary>
public sealed class PivotTable
{
    private readonly List<PivotField> _fields = [];
    private readonly List<PivotDataField> _dataFields = [];

    internal PivotTable(string name, int cacheId, CellRange sourceRange, CellReference target, string sourceSheet)
    {
        Name = name;
        CacheId = cacheId;
        SourceRange = sourceRange;
        TargetCell = target;
        SourceSheetName = sourceSheet;
    }

    /// <summary>The pivot table name.</summary>
    public string Name { get; set; }

    /// <summary>The pivot cache id linking the table to its cache definition.</summary>
    public int CacheId { get; internal set; }

    /// <summary>The source data range.</summary>
    public CellRange SourceRange { get; internal set; }

    /// <summary>The name of the worksheet the source range lives on.</summary>
    public string SourceSheetName { get; internal set; }

    /// <summary>The top-left cell where the pivot table is placed.</summary>
    public CellReference TargetCell { get; set; }

    /// <summary>All fields available from the source range.</summary>
    public IReadOnlyList<PivotField> Fields => _fields;

    /// <summary>The fields placed on the row axis, in order.</summary>
    public IEnumerable<PivotField> RowFields => _fields.Where(f => f.Axis == PivotAxis.Row);

    /// <summary>The fields placed on the column axis, in order.</summary>
    public IEnumerable<PivotField> ColumnFields => _fields.Where(f => f.Axis == PivotAxis.Column);

    /// <summary>The fields placed on the page (report filter) axis.</summary>
    public IEnumerable<PivotField> PageFields => _fields.Where(f => f.Axis == PivotAxis.Page);

    /// <summary>The data (values) fields.</summary>
    public IReadOnlyList<PivotDataField> DataFields => _dataFields;

    /// <summary>The cached source records (one inner list per source row, excluding the header).</summary>
    internal List<List<PivotCacheValue>> CacheRecords { get; } = [];

    // ── Part identity (assigned on write) ──────────────────────────────────────
    internal string TablePartUri { get; set; } = string.Empty;
    internal string TableRelationshipId { get; set; } = string.Empty;
    internal string CacheDefinitionUri { get; set; } = string.Empty;
    internal string CacheRecordsUri { get; set; } = string.Empty;
    internal string CacheDefinitionRelId { get; set; } = string.Empty;

    /// <summary>Raw preserved part bytes when loaded from a file (used for verbatim round-trip).</summary>
    internal byte[]? TablePartData { get; set; }
    internal byte[]? CacheDefinitionData { get; set; }
    internal byte[]? CacheRecordsData { get; set; }

    /// <summary>Places <paramref name="fieldName" /> on the row axis.</summary>
    public void AddRowField(string fieldName) => SetAxis(fieldName, PivotAxis.Row);

    /// <summary>Places <paramref name="fieldName" /> on the column axis.</summary>
    public void AddColumnField(string fieldName) => SetAxis(fieldName, PivotAxis.Column);

    /// <summary>Places <paramref name="fieldName" /> on the page (report filter) axis.</summary>
    public void AddPageField(string fieldName) => SetAxis(fieldName, PivotAxis.Page);

    /// <summary>Adds <paramref name="fieldName" /> as a data (values) field with the given function.</summary>
    public PivotDataField AddDataField(string fieldName, PivotDataFunction function = PivotDataFunction.Sum)
    {
        var field = FindField(fieldName);
        field.Axis = PivotAxis.Data;
        var dataField = new PivotDataField($"{FunctionLabel(function)} of {field.Name}", field.SourceIndex, function);
        _dataFields.Add(dataField);
        return dataField;
    }

    /// <summary>Removes a field from all axes (and the data area).</summary>
    public void RemoveField(string fieldName)
    {
        var field = FindField(fieldName);
        field.Axis = PivotAxis.None;
        _dataFields.RemoveAll(d => d.SourceIndex == field.SourceIndex);
    }

    /// <summary>
    ///     Rebuilds the cache (field item lists + records) from the current source-range data.
    ///     Modifies neither the layout nor the displayed grid — only the underlying cache.
    /// </summary>
    public void Refresh(SpreadsheetCallback resolveSheet)
    {
        var sheet = resolveSheet(SourceSheetName);
        if (sheet is null)
            return;

        CacheRecords.Clear();
        foreach (var field in _fields)
            field.Items.Clear();

        var topRow = SourceRange.TopLeft.Row;
        var leftCol = SourceRange.TopLeft.Column;

        for (var r = topRow + 1; r <= SourceRange.BottomRight.Row; r++)
        {
            var record = new List<PivotCacheValue>(_fields.Count);
            for (var c = leftCol; c <= SourceRange.BottomRight.Column; c++)
            {
                var cell = sheet.GetCell(r, c);
                record.Add(PivotCacheValue.FromCell(cell));

                var field = _fields[c - leftCol];
                var label = record[^1].ToString();
                if (!field.Items.Contains(label))
                    field.Items.Add(label);
            }

            CacheRecords.Add(record);
        }

        // Refreshing invalidates any preserved raw bytes — regenerate on save.
        TablePartData = null;
        CacheDefinitionData = null;
        CacheRecordsData = null;
    }

    /// <summary>Resolves a worksheet by name (supplied by the worksheet collection).</summary>
    public delegate Worksheets.Worksheet? SpreadsheetCallback(string sheetName);

    internal void AddFieldRaw(PivotField field) => _fields.Add(field);
    internal void AddDataFieldRaw(PivotDataField field) => _dataFields.Add(field);

    private void SetAxis(string fieldName, PivotAxis axis) => FindField(fieldName).Axis = axis;

    private PivotField FindField(string fieldName) =>
        _fields.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"No pivot field named '{fieldName}'.", nameof(fieldName));

    private static string FunctionLabel(PivotDataFunction f) => f switch
    {
        PivotDataFunction.Sum => "Sum",
        PivotDataFunction.Count => "Count",
        PivotDataFunction.Average => "Average",
        PivotDataFunction.Max => "Max",
        PivotDataFunction.Min => "Min",
        PivotDataFunction.Product => "Product",
        PivotDataFunction.CountNumbers => "Count",
        PivotDataFunction.StdDev => "StdDev",
        PivotDataFunction.StdDevP => "StdDevp",
        PivotDataFunction.Var => "Var",
        PivotDataFunction.VarP => "Varp",
        _ => "Sum"
    };
}
