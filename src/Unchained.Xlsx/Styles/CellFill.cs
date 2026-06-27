using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Models;

namespace Unchained.Xlsx.Styles;

/// <summary>The fill of a cell background, mapping to a <c>&lt;fill&gt;</c> entry in the styles table.</summary>
public sealed class CellFill : IEquatable<CellFill>
{
    /// <summary>The fill pattern type.</summary>
    public FillPattern PatternType { get; set; } = FillPattern.None;

    /// <summary>The foreground colour. For a solid fill this is the visible colour.</summary>
    public ColorSpec? ForegroundColor { get; set; }

    /// <summary>The background colour, used by non-solid patterns.</summary>
    public ColorSpec? BackgroundColor { get; set; }

    /// <summary>The empty (no fill) instance.</summary>
    public static CellFill None => new();

    /// <inheritdoc />
    public bool Equals(CellFill? other) =>
        other != null &&
        PatternType == other.PatternType &&
        Nullable.Equals(ForegroundColor, other.ForegroundColor) &&
        Nullable.Equals(BackgroundColor, other.BackgroundColor);

    /// <summary>Returns a fill that paints the cell with a single solid colour.</summary>
    public static CellFill Solid(ColorSpec color) =>
        new() { PatternType = FillPattern.Solid, ForegroundColor = color };

    /// <summary>Returns a shallow copy of this fill.</summary>
    public CellFill Clone() => (CellFill)MemberwiseClone();

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CellFill);

    /// <inheritdoc />
    // ReSharper disable NonReadonlyMemberInGetHashCode
    public override int GetHashCode() => HashCode.Combine(PatternType, ForegroundColor, BackgroundColor);
    // ReSharper restore NonReadonlyMemberInGetHashCode
}
