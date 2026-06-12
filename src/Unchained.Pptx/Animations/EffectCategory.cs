namespace Unchained.Pptx.Animations;

/// <summary>
///     The behavioural category of an animation effect.
///     Maps to the OOXML <c>presetClass</c> attribute on <c>&lt;p:cTn&gt;</c>.
/// </summary>
public enum EffectCategory
{
    /// <summary>Shape becomes visible. OOXML: <c>entr</c></summary>
    Entrance,
    /// <summary>Shape becomes invisible. OOXML: <c>exit</c></summary>
    Exit,
    /// <summary>Shape draws attention without appearing or disappearing. OOXML: <c>emph</c></summary>
    Emphasis,
    /// <summary>Shape moves along a path. OOXML: <c>path</c></summary>
    Motion
}
