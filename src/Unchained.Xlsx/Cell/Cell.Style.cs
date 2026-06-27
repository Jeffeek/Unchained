using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Styles;

namespace Unchained.Xlsx.Cell;

public sealed partial class Cell
{
    private StyleBook StyleBook => _worksheet.Document.Styles;

    /// <summary>The effective number-format code applied to this cell (e.g. "0.00", "General").</summary>
    public string NumberFormatCode => StyleBook.GetNumberFormatCode(StyleIndex);

    // ── Merge ────────────────────────────────────────────────────────────────

    /// <summary>The merged range whose top-left corner is this cell, or <see langword="null" />.</summary>
    public CellRange? MergeRange =>
        _worksheet.MergedCells.Cast<CellRange?>().FirstOrDefault(r => r!.Value.TopLeft == Reference);

    /// <summary><see langword="true" /> when this cell lies within any merged range.</summary>
    public bool IsMerged => _worksheet.MergedCells.RangeContaining(Reference) != null;

    // ── Effective style resolution ──────────────────────────────────────────────

    /// <summary>Returns the resolved cell format that <see cref="StyleIndex" /> points at.</summary>
    public CellXf GetEffectiveStyle() => StyleBook.GetCellXf(StyleIndex);

    // ── Mutating style helpers ───────────────────────────────────────────────────

    /// <summary>
    ///     Configures the cell's font. The current font is copied into a mutable builder, passed to
    ///     <paramref name="configure" />, then deduplicated into the style book and assigned to this cell.
    /// </summary>
    public void ApplyFont(Action<CellFont> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var font = StyleBook.GetFont(StyleIndex).Clone();
        configure(font);
        var fontId = StyleBook.GetOrAddFont(font);
        StyleIndex = WithModifiedXf(xf =>
            {
                xf.FontId = fontId;
                xf.ApplyFont = true;
            }
        );
    }

    /// <summary>Configures the cell's fill.</summary>
    public void ApplyFill(Action<CellFill> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var fill = StyleBook.GetFill(StyleIndex).Clone();
        configure(fill);
        var fillId = StyleBook.GetOrAddFill(fill);
        StyleIndex = WithModifiedXf(xf =>
            {
                xf.FillId = fillId;
                xf.ApplyFill = true;
            }
        );
    }

    /// <summary>Configures the cell's borders.</summary>
    public void ApplyBorder(Action<CellBorder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var border = StyleBook.GetBorder(StyleIndex).Clone();
        configure(border);
        var borderId = StyleBook.GetOrAddBorder(border);
        StyleIndex = WithModifiedXf(xf =>
            {
                xf.BorderId = borderId;
                xf.ApplyBorder = true;
            }
        );
    }

    /// <summary>Configures the cell's alignment.</summary>
    public void ApplyAlignment(Action<CellAlignment> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var alignment = GetEffectiveStyle().Alignment.Clone();
        configure(alignment);
        StyleIndex = WithModifiedXf(xf =>
            {
                xf.Alignment = alignment;
                xf.ApplyAlignment = true;
            }
        );
    }

    /// <summary>Assigns a number format code to this cell, registering it in the style book if new.</summary>
    public void SetNumberFormat(string formatCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(formatCode);
        var numFmtId = StyleBook.GetOrAddNumberFormat(formatCode);
        StyleIndex = WithModifiedXf(xf =>
            {
                xf.NumberFormatId = numFmtId;
                xf.ApplyNumberFormat = true;
            }
        );
    }

    /// <summary>Copies the entire style of <paramref name="source" /> onto this cell.</summary>
    public void CopyStyleFrom(Cell source)
    {
        ArgumentNullException.ThrowIfNull(source);
        StyleIndex = source.StyleIndex;
    }

    private int WithModifiedXf(Action<CellXf> modify)
    {
        var xf = GetEffectiveStyle().Clone();
        modify(xf);
        return StyleBook.GetOrAddCellXf(xf);
    }
}
