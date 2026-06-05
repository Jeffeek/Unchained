using Unchained.Pptx.Themes;

namespace Unchained.Pptx.Drawing;

/// <summary>
/// Describes the fill applied to a shape or text outline, supporting solid colour,
/// gradient, pattern, picture, and transparent (no fill) modes.
/// </summary>
public sealed class FillFormat
{
    /// <summary>The active fill type. Change this and set the corresponding sub-object.</summary>
    public Models.Shapes.FillType Type { get; set; } = Models.Shapes.FillType.None;

    /// <summary>
    /// The solid fill settings. Only meaningful when <see cref="Type"/> is
    /// <see cref="Models.Shapes.FillType.Solid"/>.
    /// </summary>
    public SolidFill? Solid { get; set; }

    /// <summary>
    /// The gradient fill settings. Only meaningful when <see cref="Type"/> is
    /// <see cref="Models.Shapes.FillType.Gradient"/>.
    /// </summary>
    public GradientFill? Gradient { get; set; }

    /// <summary>
    /// The pattern fill settings. Only meaningful when <see cref="Type"/> is
    /// <see cref="Models.Shapes.FillType.Pattern"/>.
    /// </summary>
    public PatternFill? Pattern { get; set; }

    /// <summary>
    /// The picture fill settings. Only meaningful when <see cref="Type"/> is
    /// <see cref="Models.Shapes.FillType.Picture"/>.
    /// </summary>
    public PictureFill? Picture { get; set; }

    // ── Fluent mutators ──────────────────────────────────────────────────────

    /// <summary>Sets the fill to a single solid colour and returns the new settings.</summary>
    public SolidFill SetSolid(ColorSpec color)
    {
        Type = Models.Shapes.FillType.Solid;
        Solid = new SolidFill { Color = color };
        Gradient = null;
        Pattern = null;
        Picture = null;
        return Solid;
    }

    /// <summary>Sets the fill to a gradient and returns a new empty <see cref="GradientFill"/> to configure.</summary>
    public GradientFill SetGradient()
    {
        Type = Models.Shapes.FillType.Gradient;
        Gradient = new GradientFill();
        Solid = null;
        Pattern = null;
        Picture = null;
        return Gradient;
    }

    /// <summary>Removes all fill (makes the shape transparent).</summary>
    public void SetNone()
    {
        Type = Models.Shapes.FillType.None;
        Solid = null;
        Gradient = null;
        Pattern = null;
        Picture = null;
    }
}
