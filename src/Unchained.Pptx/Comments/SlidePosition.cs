using Unchained.Ooxml;

namespace Unchained.Pptx.Comments;

/// <summary>
///     A position on a slide in EMU coordinates, used to anchor comments.
/// </summary>
public readonly struct SlidePosition
{
    /// <summary>The horizontal offset from the slide's top-left corner.</summary>
    public Emu X { get; }

    /// <summary>The vertical offset from the slide's top-left corner.</summary>
    public Emu Y { get; }

    /// <param name="x">Horizontal offset in EMU.</param>
    /// <param name="y">Vertical offset in EMU.</param>
    public SlidePosition(Emu x, Emu y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Deconstructs the position into its components.</summary>
    public void Deconstruct(out Emu x, out Emu y)
    {
        x = X;
        y = Y;
    }
}
