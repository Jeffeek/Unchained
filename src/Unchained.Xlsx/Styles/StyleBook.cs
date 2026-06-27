using Unchained.Xlsx.Cell;
using Unchained.Xlsx.Formatting;
using Unchained.Xlsx.Models.Styles;

namespace Unchained.Xlsx.Styles;

/// <summary>
///     The workbook's central style registry, owning every table in <c>xl/styles.xml</c>: fonts,
///     fills, borders, number formats, the <c>cellXfs</c> and <c>cellStyleXfs</c> format tables, and
///     the named cell styles. All mutators use get-or-add deduplication so applying the same style to
///     many cells produces a single shared entry rather than thousands of duplicates.
/// </summary>
public sealed class StyleBook
{
    private readonly List<CellFont> _fonts = [];
    private readonly List<CellFill> _fills = [];
    private readonly List<CellBorder> _borders = [];
    private readonly List<NumberFormat> _numberFormats = [];
    private readonly List<CellXf> _cellXfs = [];
    private readonly List<CellXf> _cellStyleXfs = [];
    private readonly List<NamedCellStyle> _namedStyles = [];

    private readonly Dictionary<CellFont, int> _fontLookup = new();
    private readonly Dictionary<CellFill, int> _fillLookup = new();
    private readonly Dictionary<CellBorder, int> _borderLookup = new();
    private readonly Dictionary<string, int> _numberFormatLookup = new(StringComparer.Ordinal);
    private readonly Dictionary<CellXf, int> _cellXfLookup = new();

    internal StyleBook() { }

    /// <summary>The font table.</summary>
    public IReadOnlyList<CellFont> Fonts => _fonts;

    /// <summary>The fill table.</summary>
    public IReadOnlyList<CellFill> Fills => _fills;

    /// <summary>The border table.</summary>
    public IReadOnlyList<CellBorder> Borders => _borders;

    /// <summary>The number-format table (custom formats only; built-ins are implicit).</summary>
    public IReadOnlyList<NumberFormat> NumberFormats => _numberFormats;

    /// <summary>The <c>cellXfs</c> table — the table a cell's <see cref="Cell.StyleIndex" /> points into.</summary>
    public IReadOnlyList<CellXf> CellXfs => _cellXfs;

    /// <summary>The <c>cellStyleXfs</c> table — the base formats referenced by named styles.</summary>
    public IReadOnlyList<CellXf> CellStyleXfs => _cellStyleXfs;

    /// <summary>The named cell styles (e.g. "Normal").</summary>
    public IReadOnlyList<NamedCellStyle> NamedStyles => _namedStyles;

    /// <summary><see langword="true" /> once any table changed and the part must be rewritten on save.</summary>
    internal bool IsDirty { get; private set; }

    private int _nextCustomFormatId = NumberFormat.FirstCustomId;

    // ── Creation ────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Builds a minimal valid style book: the default font, the two required placeholder fills
    ///     (<c>none</c> + <c>gray125</c>), an empty border, and a single "Normal" cell style at index 0.
    /// </summary>
    internal static StyleBook CreateDefault()
    {
        var book = new StyleBook();
        book._fonts.Add(new CellFont());
        book._fills.Add(new CellFill { PatternType = FillPattern.None });
        book._fills.Add(new CellFill { PatternType = FillPattern.Gray125 });
        book._borders.Add(new CellBorder());
        book._cellStyleXfs.Add(new CellXf());
        book._cellXfs.Add(new CellXf());
        book._namedStyles.Add(new NamedCellStyle("Normal", 0, builtInId: 0));
        book.RebuildLookups();
        return book;
    }

    // ── Get-or-add table entries ─────────────────────────────────────────────

    /// <summary>Returns the index of an equal font, or appends <paramref name="font" /> and returns the new index.</summary>
    public int GetOrAddFont(CellFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (_fontLookup.TryGetValue(font, out var index))
            return index;

        index = _fonts.Count;
        var stored = font.Clone();
        _fonts.Add(stored);
        _fontLookup[stored] = index;
        IsDirty = true;
        return index;
    }

    /// <summary>Returns the index of an equal fill, or appends <paramref name="fill" /> and returns the new index.</summary>
    public int GetOrAddFill(CellFill fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        if (_fillLookup.TryGetValue(fill, out var index))
            return index;

        index = _fills.Count;
        var stored = fill.Clone();
        _fills.Add(stored);
        _fillLookup[stored] = index;
        IsDirty = true;
        return index;
    }

    /// <summary>Returns the index of an equal border, or appends <paramref name="border" /> and returns the new index.</summary>
    public int GetOrAddBorder(CellBorder border)
    {
        ArgumentNullException.ThrowIfNull(border);
        if (_borderLookup.TryGetValue(border, out var index))
            return index;

        index = _borders.Count;
        var stored = border.Clone();
        _borders.Add(stored);
        _borderLookup[stored] = index;
        IsDirty = true;
        return index;
    }

    /// <summary>
    ///     Returns the number-format id for <paramref name="formatCode" />, reusing a built-in id when
    ///     the code matches one, an existing custom id when already registered, or allocating a new
    ///     custom id (≥ 164) otherwise.
    /// </summary>
    public int GetOrAddNumberFormat(string formatCode)
    {
        ArgumentNullException.ThrowIfNull(formatCode);

        foreach (var (id, code) in BuiltInNumberFormats.Codes)
        {
            if (string.Equals(code, formatCode, StringComparison.Ordinal))
                return id;
        }

        if (_numberFormatLookup.TryGetValue(formatCode, out var existing))
            return existing;

        var newId = _nextCustomFormatId++;
        _numberFormats.Add(new NumberFormat(newId, formatCode));
        _numberFormatLookup[formatCode] = newId;
        IsDirty = true;
        return newId;
    }

    /// <summary>Returns the index of an equal cell format, or appends <paramref name="xf" /> and returns the new index.</summary>
    public int GetOrAddCellXf(CellXf xf)
    {
        ArgumentNullException.ThrowIfNull(xf);
        if (_cellXfLookup.TryGetValue(xf, out var index))
            return index;

        index = _cellXfs.Count;
        var stored = xf.Clone();
        _cellXfs.Add(stored);
        _cellXfLookup[stored] = index;
        IsDirty = true;
        return index;
    }

    // ── Resolution ──────────────────────────────────────────────────────────────

    /// <summary>Returns the cell format at <paramref name="styleIndex" />, or the default format when out of range.</summary>
    public CellXf GetCellXf(int styleIndex) =>
        styleIndex >= 0 && styleIndex < _cellXfs.Count ? _cellXfs[styleIndex] : new CellXf();

    /// <summary>Returns the font referenced by the format at <paramref name="styleIndex" />.</summary>
    public CellFont GetFont(int styleIndex)
    {
        var xf = GetCellXf(styleIndex);
        return xf.FontId >= 0 && xf.FontId < _fonts.Count ? _fonts[xf.FontId] : new CellFont();
    }

    /// <summary>Returns the fill referenced by the format at <paramref name="styleIndex" />.</summary>
    public CellFill GetFill(int styleIndex)
    {
        var xf = GetCellXf(styleIndex);
        return xf.FillId >= 0 && xf.FillId < _fills.Count ? _fills[xf.FillId] : new CellFill();
    }

    /// <summary>Returns the border referenced by the format at <paramref name="styleIndex" />.</summary>
    public CellBorder GetBorder(int styleIndex)
    {
        var xf = GetCellXf(styleIndex);
        return xf.BorderId >= 0 && xf.BorderId < _borders.Count ? _borders[xf.BorderId] : new CellBorder();
    }

    /// <summary>Returns the effective format code for the format at <paramref name="styleIndex" />.</summary>
    public string GetNumberFormatCode(int styleIndex)
    {
        var id = GetCellXf(styleIndex).NumberFormatId;
        var builtIn = BuiltInNumberFormats.GetCode(id);
        if (builtIn != null)
            return builtIn;

        return _numberFormats.FirstOrDefault(f => f.FormatId == id)?.FormatCode ?? "General";
    }

    /// <summary>Returns the named style with the given name, or <see langword="null" /> if none.</summary>
    public NamedCellStyle? FindNamedStyle(string name) =>
        _namedStyles.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    // ── Internal loader access ───────────────────────────────────────────────

    internal void AddFontRaw(CellFont font) => _fonts.Add(font);
    internal void AddFillRaw(CellFill fill) => _fills.Add(fill);
    internal void AddBorderRaw(CellBorder border) => _borders.Add(border);
    internal void AddNumberFormatRaw(NumberFormat format) => _numberFormats.Add(format);
    internal void AddCellXfRaw(CellXf xf) => _cellXfs.Add(xf);
    internal void AddCellStyleXfRaw(CellXf xf) => _cellStyleXfs.Add(xf);
    internal void AddNamedStyleRaw(NamedCellStyle style) => _namedStyles.Add(style);

    internal void RebuildLookups()
    {
        _fontLookup.Clear();
        _fillLookup.Clear();
        _borderLookup.Clear();
        _numberFormatLookup.Clear();
        _cellXfLookup.Clear();

        for (var i = 0; i < _fonts.Count; i++) _fontLookup.TryAdd(_fonts[i], i);
        for (var i = 0; i < _fills.Count; i++) _fillLookup.TryAdd(_fills[i], i);
        for (var i = 0; i < _borders.Count; i++) _borderLookup.TryAdd(_borders[i], i);
        for (var i = 0; i < _cellXfs.Count; i++) _cellXfLookup.TryAdd(_cellXfs[i], i);

        foreach (var format in _numberFormats)
        {
            _numberFormatLookup.TryAdd(format.FormatCode, format.FormatId);
            if (format.FormatId >= _nextCustomFormatId)
                _nextCustomFormatId = format.FormatId + 1;
        }

        // Guarantee the minimal required entries exist even for sparse loaded books.
        if (_fonts.Count == 0) GetOrAddFont(new CellFont());
        if (_fills.Count < 2)
        {
            if (_fills.Count == 0) _fills.Add(new CellFill { PatternType = FillPattern.None });
            if (_fills.Count == 1) _fills.Add(new CellFill { PatternType = FillPattern.Gray125 });
            RebuildLookups();
        }

        if (_borders.Count == 0) GetOrAddBorder(new CellBorder());
        if (_cellXfs.Count == 0) _cellXfs.Add(new CellXf());
    }
}
