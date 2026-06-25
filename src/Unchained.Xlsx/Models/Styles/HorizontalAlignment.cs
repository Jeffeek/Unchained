namespace Unchained.Xlsx.Models.Styles;

/// <summary>Horizontal text alignment within a cell.</summary>
public enum HorizontalAlignment
{
    /// <summary>Default alignment (text left, numbers right).</summary>
    General,

    /// <summary>Left-aligned.</summary>
    Left,

    /// <summary>Centered.</summary>
    Center,

    /// <summary>Right-aligned.</summary>
    Right,

    /// <summary>Repeat the content to fill the cell width.</summary>
    Fill,

    /// <summary>Justify text to both edges.</summary>
    Justify,

    /// <summary>Center across the selection of merged cells.</summary>
    CenterAcrossSelection,

    /// <summary>Distribute text evenly across the cell width.</summary>
    Distributed
}
