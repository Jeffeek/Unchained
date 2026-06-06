namespace Unchained.Ooxml.Drawing;

/// <summary>
/// The twelve named colour slots defined in an OOXML theme colour scheme
/// (ECMA-376 §20.1.6.2).
/// </summary>
public enum ThemeColorSlot
{
    /// <summary>Dark 1 — typically black or very dark, used for text.</summary>
    Dark1,
    /// <summary>Light 1 — typically white or very light, used for backgrounds.</summary>
    Light1,
    /// <summary>Dark 2 — a secondary dark colour.</summary>
    Dark2,
    /// <summary>Light 2 — a secondary light colour.</summary>
    Light2,
    /// <summary>Accent 1 — the primary accent colour.</summary>
    Accent1,
    /// <summary>Accent 2 — the second accent colour.</summary>
    Accent2,
    /// <summary>Accent 3 — the third accent colour.</summary>
    Accent3,
    /// <summary>Accent 4 — the fourth accent colour.</summary>
    Accent4,
    /// <summary>Accent 5 — the fifth accent colour.</summary>
    Accent5,
    /// <summary>Accent 6 — the sixth accent colour.</summary>
    Accent6,
    /// <summary>The colour used for hyperlink text.</summary>
    Hyperlink,
    /// <summary>The colour used for visited (followed) hyperlink text.</summary>
    FollowedHyperlink
}
