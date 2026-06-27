namespace Unchained.Xlsx.Models.Styles;

/// <summary>The reading order (text direction) of a cell's content.</summary>
public enum ReadingOrder
{
    /// <summary>Determined by the first strong-directional character (default).</summary>
    ContextDependent,

    /// <summary>Left-to-right.</summary>
    LeftToRight,

    /// <summary>Right-to-left.</summary>
    RightToLeft
}
