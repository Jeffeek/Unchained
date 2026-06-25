using Unchained.Xlsx.Cell;

namespace Unchained.Xlsx.Styles;

/// <summary>
///     A cell format record — an entry in the <c>cellXfs</c> (or <c>cellStyleXfs</c>) table that
///     bundles indices into the font, fill, border, and number-format tables, plus alignment and the
///     per-attribute <c>apply*</c> flags. A cell's <see cref="Cell.StyleIndex" /> points at one of these.
/// </summary>
public sealed class CellXf : IEquatable<CellXf>
{
    /// <summary>Index into the number-format table (built-in id when &lt; 164).</summary>
    public int NumberFormatId { get; set; }

    /// <summary>Index into the font table.</summary>
    public int FontId { get; set; }

    /// <summary>Index into the fill table.</summary>
    public int FillId { get; set; }

    /// <summary>Index into the border table.</summary>
    public int BorderId { get; set; }

    /// <summary>Index into the <c>cellStyleXfs</c> table (the named-style base).</summary>
    public int XfId { get; set; }

    /// <summary>The cell alignment.</summary>
    public CellAlignment Alignment { get; set; } = new();

    /// <summary>Whether the number format applies (vs inheriting from the named style).</summary>
    public bool ApplyNumberFormat { get; set; }

    /// <summary>Whether the font applies.</summary>
    public bool ApplyFont { get; set; }

    /// <summary>Whether the fill applies.</summary>
    public bool ApplyFill { get; set; }

    /// <summary>Whether the border applies.</summary>
    public bool ApplyBorder { get; set; }

    /// <summary>Whether the alignment applies.</summary>
    public bool ApplyAlignment { get; set; }

    /// <summary>Returns a deep copy of this format record.</summary>
    public CellXf Clone() =>
        new()
        {
            NumberFormatId = NumberFormatId,
            FontId = FontId,
            FillId = FillId,
            BorderId = BorderId,
            XfId = XfId,
            Alignment = Alignment.Clone(),
            ApplyNumberFormat = ApplyNumberFormat,
            ApplyFont = ApplyFont,
            ApplyFill = ApplyFill,
            ApplyBorder = ApplyBorder,
            ApplyAlignment = ApplyAlignment
        };

    /// <inheritdoc />
    public bool Equals(CellXf? other) =>
        other != null &&
        NumberFormatId == other.NumberFormatId &&
        FontId == other.FontId &&
        FillId == other.FillId &&
        BorderId == other.BorderId &&
        XfId == other.XfId &&
        Alignment.Equals(other.Alignment) &&
        ApplyNumberFormat == other.ApplyNumberFormat &&
        ApplyFont == other.ApplyFont &&
        ApplyFill == other.ApplyFill &&
        ApplyBorder == other.ApplyBorder &&
        ApplyAlignment == other.ApplyAlignment;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CellXf);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(NumberFormatId);
        hash.Add(FontId);
        hash.Add(FillId);
        hash.Add(BorderId);
        hash.Add(XfId);
        hash.Add(Alignment);
        hash.Add(ApplyNumberFormat);
        hash.Add(ApplyFont);
        hash.Add(ApplyFill);
        hash.Add(ApplyBorder);
        hash.Add(ApplyAlignment);
        return hash.ToHashCode();
    }
}
