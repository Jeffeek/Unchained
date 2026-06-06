namespace Unchained.Pptx.Animations;

/// <summary>
/// Common animation preset identifiers, matching the OOXML <c>presetID</c> attribute.
/// The numeric value is the <c>presetID</c> used in the XML. The same ID number has
/// different meanings for Entrance vs Exit vs Emphasis effects.
/// </summary>
public enum AnimationPreset
{
    // ── Entrance / Exit presets ───────────────────────────────────────────────

    /// <summary>Shape appears instantly. presetID=1 (Entrance: Appear; Exit: Disappear).</summary>
    Appear = 1,

    /// <summary>Shape flies in/out from an edge. presetID=2.</summary>
    Fly = 2,

    /// <summary>Shape floats in/out. presetID=3.</summary>
    Float = 3,

    /// <summary>Shape splits in/out. presetID=4.</summary>
    Split = 4,

    /// <summary>Shape wipes in/out. presetID=5.</summary>
    Wipe = 5,

    /// <summary>Shape zooms in/out. presetID=6.</summary>
    Zoom = 6,

    /// <summary>Shape swivels in/out. presetID=19.</summary>
    Swivel = 19,

    /// <summary>Shape bounces in/out. presetID=8.</summary>
    Bounce = 8,

    /// <summary>Shape fades in/out smoothly. presetID=10.</summary>
    Fade = 10,

    /// <summary>Shape dissolves in/out pixel by pixel. presetID=11.</summary>
    Dissolve = 11,

    /// <summary>Shape peeks in/out from an edge. presetID=12.</summary>
    Peek = 12,

    /// <summary>Wedge wipe in/out. presetID=13.</summary>
    Wedge = 13,

    /// <summary>Wheel spin in/out. presetID=14.</summary>
    Wheel = 14,

    /// <summary>Randomly selects an effect. presetID=15.</summary>
    RandomEffects = 15,

    /// <summary>Shape grows and turns. presetID=16.</summary>
    GrowAndTurn = 16,

    /// <summary>Strips wipe. presetID=17.</summary>
    Strips = 17,

    /// <summary>Random bars. presetID=18.</summary>
    RandomBars = 18,

    /// <summary>Shape expands. presetID=20.</summary>
    Expand = 20,

    // ── Emphasis presets (same IDs, different class) ──────────────────────────

    /// <summary>Pulse emphasis. presetID=1.</summary>
    Pulse = 1,

    /// <summary>Color pulse emphasis. presetID=2.</summary>
    ColorPulse = 2,

    /// <summary>Teeter emphasis. presetID=3.</summary>
    Teeter = 3,

    /// <summary>Spin emphasis. presetID=5.</summary>
    Spin = 5,

    /// <summary>Grow/shrink emphasis. presetID=6.</summary>
    GrowShrink = 6,
}
