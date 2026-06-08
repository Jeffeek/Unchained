using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;

namespace Unchained.Ooxml.Text;

/// <summary>
/// Configures the auto-number sequence for a paragraph that uses a numbered bullet.
/// </summary>
public sealed class NumberedBulletFormat
{
    /// <summary>The numbering style (Arabic, Roman, letter, etc.).</summary>
    public NumberedBulletStyle Style { get; set; } = NumberedBulletStyle.Arabic;

    /// <summary>
    /// The first number in the sequence. Defaults to 1.
    /// </summary>
    public int StartAt { get; set; } = 1;
}
