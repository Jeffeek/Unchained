namespace Unchained.Pptx.Animations;

/// <summary>
///     The visual effect used when advancing from one slide to the next.
///     Directional variants (e.g. <see cref="PushLeft" />) encode both the base effect
///     and the direction so that callers need only set one property.
///     Maps to the child element inside <c>&lt;p:transition&gt;</c>.
/// </summary>
public enum TransitionEffect
{
    /// <summary>No transition — the new slide appears instantly.</summary>
    None,

    // ── Non-directional ──────────────────────────────────────────────────────

    /// <summary>Instant cut with no visual effect. OOXML: <c>&lt;p:cut/&gt;</c></summary>
    Cut,
    /// <summary>Cross-fade to the next slide. OOXML: <c>&lt;p:fade/&gt;</c></summary>
    Fade,
    /// <summary>Circle expands or contracts. OOXML: <c>&lt;p:circle/&gt;</c></summary>
    Circle,
    /// <summary>Wedge wipe. OOXML: <c>&lt;p:wedge/&gt;</c></summary>
    Wedge,
    /// <summary>Wheel wipe. OOXML: <c>&lt;p:wheel/&gt;</c></summary>
    Wheel,
    /// <summary>Randomly selects a different transition each time. OOXML: <c>&lt;p:random/&gt;</c></summary>
    Random,
    /// <summary>Newsflash spin-zoom. OOXML: <c>&lt;p:newsflash/&gt;</c></summary>
    Newsflash,
    /// <summary>Morphing transition (Office 2019+). OOXML: <c>&lt;p:morph/&gt;</c></summary>
    Morph,

    // ── Push (slides the new slide on from a direction) ───────────────────────

    /// <summary>Pushes from the right (new slide comes from right). OOXML: <c>&lt;p:push dir="l"/&gt;</c></summary>
    PushLeft,
    /// <summary>Pushes from the left (new slide comes from left). OOXML: <c>&lt;p:push dir="r"/&gt;</c></summary>
    PushRight,
    /// <summary>Pushes from the bottom (new slide comes from bottom). OOXML: <c>&lt;p:push dir="u"/&gt;</c></summary>
    PushUp,
    /// <summary>Pushes from the top (new slide comes from top). OOXML: <c>&lt;p:push dir="d"/&gt;</c></summary>
    PushDown,

    // ── Wipe ─────────────────────────────────────────────────────────────────

    /// <summary>Wipes from left to right. OOXML: <c>&lt;p:wipe dir="l"/&gt;</c></summary>
    WipeLeft,
    /// <summary>Wipes from right to left. OOXML: <c>&lt;p:wipe dir="r"/&gt;</c></summary>
    WipeRight,
    /// <summary>Wipes from top to bottom. OOXML: <c>&lt;p:wipe dir="u"/&gt;</c></summary>
    WipeUp,
    /// <summary>Wipes from bottom to top. OOXML: <c>&lt;p:wipe dir="d"/&gt;</c></summary>
    WipeDown,

    // ── Cover ─────────────────────────────────────────────────────────────────

    /// <summary>New slide covers old from right. OOXML: <c>&lt;p:cover dir="l"/&gt;</c></summary>
    CoverLeft,
    /// <summary>New slide covers old from left. OOXML: <c>&lt;p:cover dir="r"/&gt;</c></summary>
    CoverRight,
    /// <summary>New slide covers old from bottom. OOXML: <c>&lt;p:cover dir="u"/&gt;</c></summary>
    CoverUp,
    /// <summary>New slide covers old from top. OOXML: <c>&lt;p:cover dir="d"/&gt;</c></summary>
    CoverDown,

    // ── Uncover ───────────────────────────────────────────────────────────────

    /// <summary>Old slide uncovers to the right. OOXML: <c>&lt;p:uncover dir="l"/&gt;</c></summary>
    UncoverLeft,
    /// <summary>Old slide uncovers to the left. OOXML: <c>&lt;p:uncover dir="r"/&gt;</c></summary>
    UncoverRight,
    /// <summary>Old slide uncovers upward. OOXML: <c>&lt;p:uncover dir="u"/&gt;</c></summary>
    UncoverUp,
    /// <summary>Old slide uncovers downward. OOXML: <c>&lt;p:uncover dir="d"/&gt;</c></summary>
    UncoverDown,

    // ── Zoom ──────────────────────────────────────────────────────────────────

    /// <summary>Zooms in from the centre. OOXML: <c>&lt;p:zoom dir="in"/&gt;</c></summary>
    ZoomIn,
    /// <summary>Zooms out from the centre. OOXML: <c>&lt;p:zoom dir="out"/&gt;</c></summary>
    ZoomOut,

    // ── Blinds ────────────────────────────────────────────────────────────────

    /// <summary>Horizontal blinds. OOXML: <c>&lt;p:blinds dir="horz"/&gt;</c></summary>
    BlindsHorizontal,
    /// <summary>Vertical blinds. OOXML: <c>&lt;p:blinds dir="vert"/&gt;</c></summary>
    BlindsVertical,

    // ── Checker ───────────────────────────────────────────────────────────────

    /// <summary>Horizontal checker pattern. OOXML: <c>&lt;p:checker dir="horz"/&gt;</c></summary>
    CheckerHorizontal,
    /// <summary>Vertical checker pattern. OOXML: <c>&lt;p:checker dir="vert"/&gt;</c></summary>
    CheckerVertical,

    // ── Comb ─────────────────────────────────────────────────────────────────

    /// <summary>Horizontal comb effect. OOXML: <c>&lt;p:comb dir="horz"/&gt;</c></summary>
    CombHorizontal,
    /// <summary>Vertical comb effect. OOXML: <c>&lt;p:comb dir="vert"/&gt;</c></summary>
    CombVertical
}
