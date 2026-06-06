namespace Unchained.Ooxml.Text;

/// <summary>The style of underline applied to a text run.</summary>
public enum TextUnderlineType
{
    /// <summary>No underline.</summary>
    None,
    /// <summary>A single underline beneath the text baseline.</summary>
    Single,
    /// <summary>Two parallel underlines.</summary>
    Double,
    /// <summary>A thick single underline.</summary>
    Heavy,
    /// <summary>A dotted underline.</summary>
    Dotted,
    /// <summary>A thick dotted underline.</summary>
    DottedHeavy,
    /// <summary>A dashed underline.</summary>
    Dash,
    /// <summary>A thick dashed underline.</summary>
    DashHeavy,
    /// <summary>A long-dash underline.</summary>
    DashLong,
    /// <summary>A thick long-dash underline.</summary>
    DashLongHeavy,
    /// <summary>A dash-dot underline.</summary>
    DotDash,
    /// <summary>A thick dash-dot underline.</summary>
    DotDashHeavy,
    /// <summary>A dash-dot-dot underline.</summary>
    DotDotDash,
    /// <summary>A thick dash-dot-dot underline.</summary>
    DotDotDashHeavy,
    /// <summary>A wavy underline.</summary>
    Wavy,
    /// <summary>A thick wavy underline.</summary>
    WavyHeavy,
    /// <summary>A double wavy underline.</summary>
    WavyDouble,
    /// <summary>Underline is applied only to words (not spaces).</summary>
    Words
}
