namespace Unchained.Ooxml.Drawing;

/// <summary>
/// The DrawingML effect list applied to a shape, picture, or text run — outer/inner shadow,
/// glow, reflection, soft edges, and blur. Mirrors the OOXML <c>&lt;a:effectLst&gt;</c>.
/// Each effect is <see langword="null"/> when not present.
/// </summary>
public sealed class EffectFormat
{
    /// <summary>Outer (drop) shadow, cast outside the shape. <see langword="null"/> when absent.</summary>
    public OuterShadowEffect? OuterShadow { get; set; }

    /// <summary>Inner shadow, cast inside the shape edges. <see langword="null"/> when absent.</summary>
    public InnerShadowEffect? InnerShadow { get; set; }

    /// <summary>Glow halo around the shape. <see langword="null"/> when absent.</summary>
    public GlowEffect? Glow { get; set; }

    /// <summary>Mirror reflection below the shape. <see langword="null"/> when absent.</summary>
    public ReflectionEffect? Reflection { get; set; }

    /// <summary>Soft (feathered) edge radius. <see langword="null"/> when absent.</summary>
    public SoftEdgeEffect? SoftEdge { get; set; }

    /// <summary>Blur applied to the whole shape. <see langword="null"/> when absent.</summary>
    public BlurEffect? Blur { get; set; }

    /// <summary><see langword="true"/> when no effects are set.</summary>
    public bool IsEmpty =>
        OuterShadow is null && InnerShadow is null && Glow is null &&
        Reflection is null && SoftEdge is null && Blur is null;
}

/// <summary>An outer (drop) shadow effect (<c>&lt;a:outerShdw&gt;</c>).</summary>
public sealed class OuterShadowEffect
{
    /// <summary>Shadow colour.</summary>
    public ColorSpec Color { get; set; } = ColorSpec.FromArgb(0x80, 0, 0, 0);

    /// <summary>Blur radius in EMU.</summary>
    public Emu BlurRadius { get; set; }

    /// <summary>Shadow offset distance in EMU.</summary>
    public Emu Distance { get; set; }

    /// <summary>Direction of the shadow offset, in degrees (0 = right, clockwise).</summary>
    public double DirectionDegrees { get; set; }

    /// <summary>Horizontal scale percentage (100 = unscaled).</summary>
    public double ScaleHorizontalPercent { get; set; } = 100;

    /// <summary>Vertical scale percentage (100 = unscaled).</summary>
    public double ScaleVerticalPercent { get; set; } = 100;

    /// <summary>Alignment anchor for the shadow (e.g. <c>tl</c>, <c>ctr</c>, <c>br</c>).</summary>
    public string Alignment { get; set; } = "tl";

    /// <summary>Whether the shadow rotates with the shape.</summary>
    public bool RotateWithShape { get; set; }
}

/// <summary>An inner shadow effect (<c>&lt;a:innerShdw&gt;</c>).</summary>
public sealed class InnerShadowEffect
{
    /// <summary>Shadow colour.</summary>
    public ColorSpec Color { get; set; } = ColorSpec.FromArgb(0x80, 0, 0, 0);

    /// <summary>Blur radius in EMU.</summary>
    public Emu BlurRadius { get; set; }

    /// <summary>Shadow offset distance in EMU.</summary>
    public Emu Distance { get; set; }

    /// <summary>Direction of the shadow offset, in degrees.</summary>
    public double DirectionDegrees { get; set; }
}

/// <summary>A glow effect (<c>&lt;a:glow&gt;</c>).</summary>
public sealed class GlowEffect
{
    /// <summary>Glow colour.</summary>
    public ColorSpec Color { get; set; } = ColorSpec.FromRgb(0xFF, 0xFF, 0x00);

    /// <summary>Glow radius in EMU.</summary>
    public Emu Radius { get; set; }
}

/// <summary>A reflection effect (<c>&lt;a:reflection&gt;</c>).</summary>
public sealed class ReflectionEffect
{
    /// <summary>Blur radius in EMU.</summary>
    public Emu BlurRadius { get; set; }

    /// <summary>Starting opacity percentage of the reflection (0–100).</summary>
    public double StartOpacityPercent { get; set; } = 100;

    /// <summary>Ending opacity percentage of the reflection (0–100).</summary>
    public double EndOpacityPercent { get; set; }

    /// <summary>Distance of the reflection from the shape in EMU.</summary>
    public Emu Distance { get; set; }

    /// <summary>Direction of the reflection offset in degrees.</summary>
    public double DirectionDegrees { get; set; }
}

/// <summary>A soft-edge (feathered) effect (<c>&lt;a:softEdge&gt;</c>).</summary>
public sealed class SoftEdgeEffect
{
    /// <summary>Feather radius in EMU.</summary>
    public Emu Radius { get; set; }
}

/// <summary>A blur effect (<c>&lt;a:blur&gt;</c>).</summary>
public sealed class BlurEffect
{
    /// <summary>Blur radius in EMU.</summary>
    public Emu Radius { get; set; }

    /// <summary>Whether the bounds grow to fit the blur.</summary>
    public bool GrowBounds { get; set; } = true;
}
