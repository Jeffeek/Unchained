namespace Unchained.Ooxml.Drawing;

/// <summary>
/// A fill that tiles two colours in a repeating geometric pattern.
/// </summary>
public sealed class PatternFill
{
    /// <summary>The preset pattern type.</summary>
    public PatternPreset Preset { get; set; }

    /// <summary>The foreground colour of the pattern.</summary>
    public ColorSpec ForegroundColor { get; set; }

    /// <summary>The background colour of the pattern.</summary>
    public ColorSpec BackgroundColor { get; set; }
}
