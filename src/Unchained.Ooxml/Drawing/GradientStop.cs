namespace Unchained.Ooxml.Drawing;

/// <summary>
/// One colour stop in a gradient fill, combining a colour with its position along the gradient axis.
/// </summary>
/// <param name="Position">
/// The position along the gradient axis, from 0.0 (start) to 1.0 (end).
/// In OOXML this is stored as an integer in the range 0–100,000; this struct uses the normalised double.
/// </param>
/// <param name="Color">The colour at this stop.</param>
public readonly record struct GradientStop(double Position, ColorSpec Color);
