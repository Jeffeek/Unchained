using Unchained.Pptx.Models.Text;
using Unchained.Pptx.Themes;

namespace Unchained.Pptx.Text;

/// <summary>
/// Defines the bullet (list marker) for a paragraph.
/// </summary>
public sealed class BulletFormat
{
    /// <summary>The bullet type. <see cref="BulletType.None"/> means no bullet is shown.</summary>
    public BulletType Type { get; set; } = BulletType.None;

    /// <summary>
    /// The character used as the bullet when <see cref="Type"/> is <see cref="BulletType.Character"/>.
    /// </summary>
    public string? Character { get; set; }

    /// <summary>
    /// The font applied to the bullet character.
    /// <see langword="null"/> means inherit from the run format.
    /// </summary>
    public string? Font { get; set; }

    /// <summary>
    /// The bullet colour. <see langword="null"/> means inherit from the text colour.
    /// </summary>
    public ColorSpec? Color { get; set; }

    /// <summary>
    /// The bullet size as a percentage of the paragraph font size (e.g. 100 = same size).
    /// <see langword="null"/> means inherit.
    /// </summary>
    public double? SizePercent { get; set; }

    /// <summary>
    /// Auto-number settings. Only meaningful when <see cref="Type"/> is
    /// <see cref="BulletType.Numbered"/>.
    /// </summary>
    public NumberedBulletFormat? Numbered { get; set; }
}
