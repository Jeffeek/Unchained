using Unchained.Pptx.Core;
using Unchained.Pptx.Models.Text;

namespace Unchained.Pptx.Text;

/// <summary>
/// A paragraph within a <see cref="TextFrame"/>, containing an ordered sequence of
/// <see cref="Run"/> objects with shared paragraph-level formatting.
/// </summary>
public sealed class Paragraph
{
    /// <summary>The text runs that make up this paragraph.</summary>
    public RunCollection Runs { get; } = new();

    /// <summary>
    /// Gets or sets the paragraph's text as a plain string.
    /// <para>
    /// Getter: concatenates all run texts in order.
    /// Setter: replaces all existing runs with a single run containing the given text.
    /// </para>
    /// </summary>
    public string PlainText
    {
        get => string.Concat(Runs.Select(static r => r.Text));
        set
        {
            Runs.Clear();
            Runs.Add(value);
        }
    }

    // ── Paragraph formatting ─────────────────────────────────────────────────

    /// <summary>Horizontal text alignment. <see langword="null"/> means inherit.</summary>
    public TextAlignment? Alignment { get; set; }

    /// <summary>Space before the paragraph in points. <see langword="null"/> means inherit.</summary>
    public double? SpaceBeforePoints { get; set; }

    /// <summary>Space after the paragraph in points. <see langword="null"/> means inherit.</summary>
    public double? SpaceAfterPoints { get; set; }

    /// <summary>Line spacing. <see langword="null"/> means inherit (typically single-spaced).</summary>
    public LineSpacing? Spacing { get; set; }

    /// <summary>Left margin in EMU. <see langword="null"/> means inherit.</summary>
    public Core.Emu? MarginLeft { get; set; }

    /// <summary>Right margin in EMU. <see langword="null"/> means inherit.</summary>
    public Core.Emu? MarginRight { get; set; }

    /// <summary>
    /// Indent (first-line indent) in EMU. A negative value creates a hanging indent.
    /// <see langword="null"/> means inherit.
    /// </summary>
    public Core.Emu? Indent { get; set; }

    /// <summary>Outline level (0 = top level). Used for SmartArt and outline views.</summary>
    public int OutlineLevel { get; set; }

    /// <summary><see langword="true"/> when the paragraph reads right-to-left.</summary>
    public bool RightToLeft { get; set; }

    /// <summary>Bullet / list marker settings for this paragraph.</summary>
    public BulletFormat Bullet { get; } = new();
}
