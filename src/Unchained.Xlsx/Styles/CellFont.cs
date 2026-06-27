using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Models;

namespace Unchained.Xlsx.Styles;

/// <summary>The font of a cell, mapping to a <c>&lt;font&gt;</c> entry in the styles table.</summary>
public sealed class CellFont : IEquatable<CellFont>
{
    /// <summary>The font family name (e.g. "Calibri").</summary>
    public string Name { get; set; } = "Calibri";

    /// <summary>The font size in points.</summary>
    public double SizePoints { get; set; } = 11;

    /// <summary>Whether the font is bold.</summary>
    public bool Bold { get; set; }

    /// <summary>Whether the font is italic.</summary>
    public bool Italic { get; set; }

    /// <summary>The underline style.</summary>
    public FontUnderline Underline { get; set; } = FontUnderline.None;

    /// <summary>Whether a line is drawn through the text.</summary>
    public bool Strikethrough { get; set; }

    /// <summary>The font colour, or <see langword="null" /> for the theme default (text 1).</summary>
    public ColorSpec? Color { get; set; }

    /// <summary>Superscript / subscript positioning.</summary>
    public FontVerticalAlignment VerticalAlignment { get; set; } = FontVerticalAlignment.None;

    /// <summary>Whether the outline effect is applied (rarely used).</summary>
    public bool Outline { get; set; }

    /// <summary>Whether the shadow effect is applied (rarely used).</summary>
    public bool Shadow { get; set; }

    /// <summary>Whether the font is condensed (legacy Mac).</summary>
    public bool Condense { get; set; }

    /// <summary>Whether the font is extended (legacy Mac).</summary>
    public bool Extend { get; set; }

    /// <summary>The theme font scheme this font belongs to ("major", "minor"), or <see langword="null" />.</summary>
    public string? Scheme { get; set; }

    /// <inheritdoc />
    public bool Equals(CellFont? other) =>
        other != null &&
        Name == other.Name &&
        SizePoints.Equals(other.SizePoints) &&
        Bold == other.Bold &&
        Italic == other.Italic &&
        Underline == other.Underline &&
        Strikethrough == other.Strikethrough &&
        Nullable.Equals(Color, other.Color) &&
        VerticalAlignment == other.VerticalAlignment &&
        Outline == other.Outline &&
        Shadow == other.Shadow &&
        Condense == other.Condense &&
        Extend == other.Extend &&
        Scheme == other.Scheme;

    /// <summary>Returns a shallow copy of this font.</summary>
    public CellFont Clone() => (CellFont)MemberwiseClone();

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CellFont);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        // ReSharper disable NonReadonlyMemberInGetHashCode
        hash.Add(Name);
        hash.Add(SizePoints);
        hash.Add(Bold);
        hash.Add(Italic);
        hash.Add(Underline);
        hash.Add(Strikethrough);
        hash.Add(Color);
        hash.Add(VerticalAlignment);
        hash.Add(Outline);
        hash.Add(Shadow);
        hash.Add(Condense);
        hash.Add(Extend);
        hash.Add(Scheme);
        // ReSharper restore NonReadonlyMemberInGetHashCode
        return hash.ToHashCode();
    }
}
