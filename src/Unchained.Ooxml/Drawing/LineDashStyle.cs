namespace Unchained.Ooxml.Drawing;

/// <summary>Specifies the dash pattern of a line or shape outline.</summary>
public enum LineDashStyle
{
    /// <summary>A solid (unbroken) line.</summary>
    Solid,
    /// <summary>A dotted line.</summary>
    Dot,
    /// <summary>A short-dash line.</summary>
    ShortDash,
    /// <summary>Alternating short dashes and dots.</summary>
    ShortDashDot,
    /// <summary>Alternating short dashes and two dots.</summary>
    ShortDashDotDot,
    /// <summary>A long-dash line.</summary>
    Dash,
    /// <summary>Alternating long dashes and dots.</summary>
    DashDot,
    /// <summary>Alternating long dashes and two dots.</summary>
    LongDashDotDot,
    /// <summary>A long-dash line (alias for <see cref="Dash"/> used by some readers).</summary>
    LongDash,
    /// <summary>Alternating long and short dashes.</summary>
    LongDashDot,
    /// <summary>A system default dash (reader-defined).</summary>
    SystemDash,
    /// <summary>A system default dot (reader-defined).</summary>
    SystemDot,
    /// <summary>A system default dash-dot (reader-defined).</summary>
    SystemDashDot,
    /// <summary>A system default dash-dot-dot (reader-defined).</summary>
    SystemDashDotDot
}
