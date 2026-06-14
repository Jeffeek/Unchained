namespace Unchained.Ooxml.Drawing;

/// <summary>
///     A fill that transitions smoothly between two or more colours along a linear or radial path.
/// </summary>
public sealed class GradientFill
{
    /// <summary>
    ///     The ordered list of colour stops that define the gradient.
    ///     Each stop specifies a colour and its position along the gradient axis (0.0–1.0).
    /// </summary>
    public List<GradientStop> Stops { get; } = [];

    /// <summary>
    ///     The angle of a linear gradient in degrees, measured clockwise from the top.
    ///     0° = top-to-bottom, 90° = left-to-right.
    /// </summary>
    public double LinearAngleDegrees { get; set; }

    /// <summary>
    ///     <see langword="true" /> for a linear gradient; <see langword="false" /> for a path (radial) gradient.
    /// </summary>
    public bool IsLinear { get; set; } = true;
}
