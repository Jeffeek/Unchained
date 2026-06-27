namespace Unchained.Ooxml.Drawing;

/// <summary>
///     The twelve named colour slots of a presentation's theme colour scheme.
///     Each slot holds a <see cref="ColorSpec" /> that defines its colour.
/// </summary>
public sealed class ColorScheme
{
    /// <summary>Dark 1 — typically the darkest colour, used for text.</summary>
    public ColorSpec Dark1 { get; set; }

    /// <summary>Light 1 — typically the lightest colour, used for backgrounds.</summary>
    public ColorSpec Light1 { get; set; }

    /// <summary>Dark 2 — a secondary dark colour.</summary>
    public ColorSpec Dark2 { get; set; }

    /// <summary>Light 2 — a secondary light colour.</summary>
    public ColorSpec Light2 { get; set; }

    /// <summary>Accent 1 — the primary accent colour.</summary>
    public ColorSpec Accent1 { get; set; }

    /// <summary>Accent 2.</summary>
    public ColorSpec Accent2 { get; set; }

    /// <summary>Accent 3.</summary>
    public ColorSpec Accent3 { get; set; }

    /// <summary>Accent 4.</summary>
    public ColorSpec Accent4 { get; set; }

    /// <summary>Accent 5.</summary>
    public ColorSpec Accent5 { get; set; }

    /// <summary>Accent 6.</summary>
    public ColorSpec Accent6 { get; set; }

    /// <summary>The colour used for hyperlink text.</summary>
    public ColorSpec HyperlinkColor { get; set; }

    /// <summary>The colour used for visited (followed) hyperlink text.</summary>
    public ColorSpec FollowedHyperlinkColor { get; set; }

    internal const uint UnresolvedThemeColorArgb = 0xFF808080u;

    /// <summary>Gets or sets a colour slot by its <see cref="ThemeColorSlot" /> identifier.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unrecognised slot.</exception>
    public ColorSpec this[ThemeColorSlot slot]
    {
        get => slot switch
        {
            ThemeColorSlot.Dark1 => Dark1,
            ThemeColorSlot.Light1 => Light1,
            ThemeColorSlot.Dark2 => Dark2,
            ThemeColorSlot.Light2 => Light2,
            ThemeColorSlot.Accent1 => Accent1,
            ThemeColorSlot.Accent2 => Accent2,
            ThemeColorSlot.Accent3 => Accent3,
            ThemeColorSlot.Accent4 => Accent4,
            ThemeColorSlot.Accent5 => Accent5,
            ThemeColorSlot.Accent6 => Accent6,
            ThemeColorSlot.Hyperlink => HyperlinkColor,
            ThemeColorSlot.FollowedHyperlink => FollowedHyperlinkColor,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unrecognised theme colour slot.")
        };
        set
        {
            switch (slot)
            {
                case ThemeColorSlot.Dark1: Dark1 = value; break;
                case ThemeColorSlot.Light1: Light1 = value; break;
                case ThemeColorSlot.Dark2: Dark2 = value; break;
                case ThemeColorSlot.Light2: Light2 = value; break;
                case ThemeColorSlot.Accent1: Accent1 = value; break;
                case ThemeColorSlot.Accent2: Accent2 = value; break;
                case ThemeColorSlot.Accent3: Accent3 = value; break;
                case ThemeColorSlot.Accent4: Accent4 = value; break;
                case ThemeColorSlot.Accent5: Accent5 = value; break;
                case ThemeColorSlot.Accent6: Accent6 = value; break;
                case ThemeColorSlot.Hyperlink: HyperlinkColor = value; break;
                case ThemeColorSlot.FollowedHyperlink: FollowedHyperlinkColor = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unrecognised theme colour slot.");
            }
        }
    }

    /// <summary>
    ///     Resolves a theme slot to its concrete ARGB value.
    ///     Returns a neutral mid-grey (0xFF808080) if the slot has not been initialised.
    /// </summary>
    internal uint Resolve(ThemeColorSlot slot) => this[slot].Resolve(this);
}
