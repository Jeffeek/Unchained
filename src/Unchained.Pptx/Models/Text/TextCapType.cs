namespace Unchained.Pptx.Models.Text;

/// <summary>Capitalisation style applied to text runs.</summary>
public enum TextCapType
{
    /// <summary>No capitalisation override; text appears as authored.</summary>
    None,
    /// <summary>Lowercase letters are rendered as smaller uppercase letters.</summary>
    SmallCaps,
    /// <summary>All characters are rendered as uppercase letters.</summary>
    AllCaps
}
