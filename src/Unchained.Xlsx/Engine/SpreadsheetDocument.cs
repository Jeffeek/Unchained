using Unchained.Ooxml.Opc;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.DefinedNames;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Security;
using Unchained.Xlsx.SharedStrings;
using Unchained.Xlsx.Styles;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Engine;

/// <summary>
///     An in-memory Excel workbook: the root object of the Unchained.Xlsx model.
///     Load one with <see cref="SpreadsheetProcessor.LoadAsync(string, OpenOptions?, CancellationToken)" />
///     or create one with <see cref="SpreadsheetProcessor.CreateBlank()" />.
/// </summary>
/// <remarks>
///     The document retains the originating OPC package so that parts Unchained does not model
///     (theme, custom XML, query tables, sparklines, …) round-trip unchanged on save. Dispose the
///     document to release that package.
/// </remarks>
public sealed class SpreadsheetDocument : ISpreadsheetDocument
{
    private bool _disposed;

    internal SpreadsheetDocument(OpcPackage? package = null)
    {
        Package = package;
        Sheets = new WorksheetCollection(this);
        Properties = new WorkbookProperties();
        DefinedNames = new DefinedNameCollection();
        Protection = new WorkbookProtection();
    }

    /// <summary>
    ///     The backing OPC package, when this document was loaded from one. Used to preserve
    ///     unmodeled parts on save. <see langword="null" /> for freshly created documents.
    /// </summary>
    internal OpcPackage? Package { get; }

    /// <summary>The worksheets in this workbook, in tab order.</summary>
    public WorksheetCollection Sheets { get; }

    /// <summary>The workbook metadata (core + extended properties).</summary>
    public WorkbookProperties Properties { get; set; }

    /// <summary>The workbook's defined names (named ranges).</summary>
    public DefinedNameCollection DefinedNames { get; }

    /// <summary>The workbook-level protection settings.</summary>
    public WorkbookProtection Protection { get; }

    /// <summary><see langword="true" /> when this workbook was loaded from an encrypted (password-protected) file.</summary>
    public bool WasLoadedEncrypted { get; internal set; }

    /// <summary>
    ///     Removes the in-memory encryption flag so the next save writes an unencrypted package
    ///     (unless an <see cref="XlsxSaveOptions.Password" /> is supplied). Save without a password to
    ///     produce a decrypted workbook.
    /// </summary>
    public void RemoveEncryption() => WasLoadedEncrypted = false;

    /// <summary>
    ///     <see langword="true" /> when the workbook uses the 1904 date system (legacy Mac),
    ///     in which date serial numbers are offset by 1462 days and there is no phantom 1900 leap day.
    /// </summary>
    public bool Date1904 { get; internal set; }

    /// <summary>
    ///     The workbook shared-string table, loaded lazily from <c>xl/sharedStrings.xml</c> on first
    ///     access. Always non-null; an empty table is returned for workbooks that lack the part.
    /// </summary>
    internal SharedStringsTable SharedStrings
    {
        get
        {
            if (field != null)
                return field;

            var part = Package?.Parts.FirstOrDefault(static p => p.ContentType.Equals(Core.Xml.SmlNames.ContentTypeSharedStrings, StringComparison.Ordinal));
            field = SharedStringsTable.Parse(part?.Data);
            return field;
        }
    }

    /// <summary>
    ///     The workbook style registry (fonts, fills, borders, number formats, format tables),
    ///     loaded lazily from <c>xl/styles.xml</c> on first access.
    /// </summary>
    public StyleBook Styles
    {
        get
        {
            if (MaterialisedStyles != null)
                return MaterialisedStyles;

            var part = Package?.Parts.FirstOrDefault(static p =>
                p.ContentType.Equals(Core.Xml.SmlNames.ContentTypeStyles, StringComparison.Ordinal)
            );
            MaterialisedStyles = part != null
                ? Parsing.StylesParser.Parse(part.Data)
                : StyleBook.CreateDefault();
            return MaterialisedStyles;
        }
    }

    /// <summary>The style book if it has been materialised (loaded or accessed), otherwise <see langword="null" />.</summary>
    internal StyleBook? MaterialisedStyles { get; private set; }

    /// <summary>
    ///     When <see langword="true" />, the workbook is flagged so that the spreadsheet application
    ///     recalculates every formula the next time it is opened. Set by <see cref="RecalculateAll" />.
    /// </summary>
    internal bool ForceFullRecalc { get; private set; }

    /// <summary>
    ///     Marks every formula in the workbook for recalculation on next open. Unchained does not
    ///     evaluate formulas; this only sets the workbook's <c>fullCalcOnLoad</c> flag.
    /// </summary>
    public void RecalculateAll() => ForceFullRecalc = true;

    /// <summary>
    ///     Evaluates every formula cell in-engine and stores each computed result as the cell's
    ///     cached value, so the value getters reflect the result without a spreadsheet application.
    ///     Supports the common function library (see <c>FormulaFunctions</c>); unknown functions yield
    ///     <c>#NAME?</c> and circular references yield <c>#REF!</c>. Returns the number of formulas evaluated.
    /// </summary>
    public int Recalculate() => Formulas.FormulaCalculator.Recalculate(this);

    /// <summary>
    ///     Evaluates a single formula string (with or without a leading <c>=</c>) against
    ///     <paramref name="contextSheet" /> and returns the result as a boxed CLR value
    ///     (<see langword="double" />, <see langword="string" />, <see langword="bool" />,
    ///     <see cref="CellError" />, or <see langword="null" /> for blank).
    /// </summary>
    public static object? EvaluateFormula(Worksheet contextSheet, string formula)
    {
        ArgumentNullException.ThrowIfNull(contextSheet);
        ArgumentNullException.ThrowIfNull(formula);
        var evaluator = new Formulas.FormulaEvaluator(contextSheet);
        var result = evaluator.Evaluate(formula.StartsWith('=') ? formula[1..] : formula);
        return result.Kind switch
        {
            Formulas.FormulaValueKind.Number => result.Number,
            Formulas.FormulaValueKind.Boolean => result.Boolean,
            Formulas.FormulaValueKind.Text => result.Text,
            Formulas.FormulaValueKind.Error => result.Error,
            _ => null
        };
    }

    private int _maxTableId;

    /// <summary>Allocates the next workbook-unique table id (used for new <c>ListObject</c> parts).</summary>
    internal int NextTableId() => ++_maxTableId;

    private int _maxPivotCacheId;

    /// <summary>Allocates the next workbook-unique pivot cache id.</summary>
    internal int NextPivotCacheId() => ++_maxPivotCacheId;

    /// <summary>Records an observed pivot cache id so future allocations stay unique.</summary>
    internal void ObservePivotCacheId(int id)
    {
        if (id > _maxPivotCacheId)
            _maxPivotCacheId = id;
    }

    /// <summary>Records an observed table id so future allocations stay unique.</summary>
    internal void ObserveTableId(int id)
    {
        if (id > _maxTableId)
            _maxTableId = id;
    }

    // ── IDisposable ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Package?.Dispose();
    }
}
