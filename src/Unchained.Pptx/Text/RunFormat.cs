using Unchained.Pptx.Core;
using Unchained.Pptx.Drawing;
using Unchained.Pptx.Models.Text;
using Unchained.Pptx.Themes;

namespace Unchained.Pptx.Text;

/// <summary>
/// Character-level formatting applied to a <see cref="Run"/> within a paragraph.
/// Any property that is not explicitly set (<see langword="null"/> or
/// <see cref="InheritableBool.Inherit"/>) inherits its effective value from
/// the enclosing paragraph, slide layout, master, or theme.
/// </summary>
public sealed class RunFormat
{
    // ── Font family ──────────────────────────────────────────────────────────

    /// <summary>
    /// The Latin-script font reference. <see langword="null"/> means inherit.
    /// Use <c>"+mj-lt"</c> for the theme major Latin font, or <c>"+mn-lt"</c> for the minor font.
    /// </summary>
    public string? LatinFont { get; set; }

    /// <summary>East Asian font reference. <see langword="null"/> means inherit.</summary>
    public string? EastAsianFont { get; set; }

    /// <summary>Complex-script font reference. <see langword="null"/> means inherit.</summary>
    public string? ComplexScriptFont { get; set; }

    // ── Size ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Font size in typographic points. <see langword="null"/> means inherit.
    /// </summary>
    public double? FontSizePoints { get; set; }

    // ── Style flags ──────────────────────────────────────────────────────────

    /// <summary>Bold. <see cref="InheritableBool.Inherit"/> means inherit from the paragraph or theme.</summary>
    public InheritableBool Bold { get; set; } = InheritableBool.Inherit;

    /// <summary>Italic. <see cref="InheritableBool.Inherit"/> means inherit.</summary>
    public InheritableBool Italic { get; set; } = InheritableBool.Inherit;

    /// <summary>Underline style. <see cref="TextUnderlineType.None"/> means no underline.</summary>
    public TextUnderlineType Underline { get; set; } = TextUnderlineType.None;

    /// <summary>Strikethrough style.</summary>
    public TextStrikethrough Strikethrough { get; set; } = TextStrikethrough.None;

    /// <summary>Capitalisation style.</summary>
    public TextCapType Capitalisation { get; set; } = TextCapType.None;

    // ── Color & fill ─────────────────────────────────────────────────────────

    /// <summary>Text fill (usually a solid colour). <see langword="null"/> means inherit.</summary>
    public FillFormat? Fill { get; set; }

    // ── Spacing & position ───────────────────────────────────────────────────

    /// <summary>
    /// Character spacing adjustment in points.
    /// Positive values expand spacing; negative values compress it.
    /// <see langword="null"/> means inherit.
    /// </summary>
    public double? CharacterSpacingPoints { get; set; }

    /// <summary>
    /// Baseline shift as a percentage of the font size.
    /// Positive values shift the text upward (superscript); negative values shift downward (subscript).
    /// <see langword="null"/> means inherit.
    /// </summary>
    public double? BaselineShiftPercent { get; set; }

    // ── Language ─────────────────────────────────────────────────────────────

    /// <summary>
    /// BCP 47 language tag for spell-checking and hyphenation (e.g. <c>"en-US"</c>, <c>"ja-JP"</c>).
    /// <see langword="null"/> means inherit.
    /// </summary>
    public string? LanguageTag { get; set; }

    /// <summary>
    /// <see langword="true"/> when the run should not be spell-checked.
    /// </summary>
    public InheritableBool NoSpellCheck { get; set; } = InheritableBool.Inherit;
}
