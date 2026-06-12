namespace Unchained.Ooxml.Drawing;

/// <summary>
///     WordArt text-warp geometry applied to a text body (<c>&lt;a:bodyPr&gt;&lt;a:prstTxWarp&gt;</c>).
///     The preset name (e.g. <c>textArchUp</c>, <c>textWave1</c>, <c>textCircle</c>) determines the
///     shape the text is bent along.
/// </summary>
public sealed class TextWarpFormat
{
    /// <summary>
    ///     The preset warp name as defined by OOXML (without adornment), e.g. <c>textArchUp</c>,
    ///     <c>textWave1</c>, <c>textCircle</c>, <c>textInflate</c>, <c>textChevron</c>.
    /// </summary>
    public string Preset { get; set; } = string.Empty;
}

/// <summary>
///     3-D format applied to a shape or text (<c>&lt;a:sp3d&gt;</c>): bevels, extrusion, contour,
///     and material.
/// </summary>
public sealed class Shape3DFormat
{
    /// <summary>Top bevel. <see langword="null" /> when absent.</summary>
    public BevelFormat? TopBevel { get; set; }

    /// <summary>Bottom bevel. <see langword="null" /> when absent.</summary>
    public BevelFormat? BottomBevel { get; set; }

    /// <summary>Extrusion (depth) height in EMU.</summary>
    public Emu ExtrusionHeight { get; set; }

    /// <summary>Extrusion colour. <see langword="null" /> means inherit.</summary>
    public ColorSpec? ExtrusionColor { get; set; }

    /// <summary>Contour (edge) width in EMU.</summary>
    public Emu ContourWidth { get; set; }

    /// <summary>Contour colour. <see langword="null" /> means inherit.</summary>
    public ColorSpec? ContourColor { get; set; }

    /// <summary>Preset material (e.g. <c>matte</c>, <c>plastic</c>, <c>metal</c>).</summary>
    public string? Material { get; set; }

    /// <summary><see langword="true" /> when no 3-D settings are present.</summary>
    public bool IsEmpty =>
        TopBevel is null && BottomBevel is null && ExtrusionHeight.Value == 0 &&
        ExtrusionColor is null && ContourWidth.Value == 0 && ContourColor is null && Material is null;
}

/// <summary>A 3-D bevel definition (<c>&lt;a:bevelT&gt;</c> / <c>&lt;a:bevelB&gt;</c>).</summary>
public sealed class BevelFormat
{
    /// <summary>Bevel width in EMU.</summary>
    public Emu Width { get; set; }

    /// <summary>Bevel height in EMU.</summary>
    public Emu Height { get; set; }

    /// <summary>Preset bevel type (e.g. <c>circle</c>, <c>relaxedInset</c>, <c>slope</c>, <c>angle</c>).</summary>
    public string Preset { get; set; } = "circle";
}
