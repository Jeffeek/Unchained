namespace Unchained.Pptx.Slides;

/// <summary>Type of slide transition effect.</summary>
public enum TransitionType
{
    /// <summary>No transition.</summary>
    None,
    /// <summary>Fade transition.</summary>
    Fade,
    /// <summary>Push transition.</summary>
    Push,
    /// <summary>Wipe transition.</summary>
    Wipe,
    /// <summary>Split transition.</summary>
    Split,
    /// <summary>Reveal transition.</summary>
    Reveal
}

/// <summary>Direction of a slide transition.</summary>
public enum TransitionDirection
{
    /// <summary>Transition from top.</summary>
    FromTop,
    /// <summary>Transition from bottom.</summary>
    FromBottom,
    /// <summary>Transition from left.</summary>
    FromLeft,
    /// <summary>Transition from right.</summary>
    FromRight
}
