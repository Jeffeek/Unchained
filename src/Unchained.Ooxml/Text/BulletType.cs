namespace Unchained.Ooxml.Text;

/// <summary>The type of bullet applied to a paragraph.</summary>
public enum BulletType
{
    /// <summary>No bullet is shown.</summary>
    None,
    /// <summary>A specific character is used as the bullet.</summary>
    Character,
    /// <summary>A raster or vector image is used as the bullet.</summary>
    Picture,
    /// <summary>An automatically incremented number or letter sequence.</summary>
    Numbered
}
