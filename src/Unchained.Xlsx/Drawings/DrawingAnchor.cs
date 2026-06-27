using Unchained.Ooxml;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Drawings;

namespace Unchained.Xlsx.Drawings;

/// <summary>
///     A drawing anchor: where a picture or chart sits on the worksheet grid. A two-cell anchor
///     spans <see cref="From" />..<see cref="To" /> (with EMU offsets inside each cell); a one-cell
///     anchor pins <see cref="From" /> and uses <see cref="Width" />/<see cref="Height" />; an
///     absolute anchor uses <see cref="OffsetX" />/<see cref="OffsetY" /> + size.
/// </summary>
/// <remarks>
///     Cell-grid coordinates in the public API are 1-based (matching <see cref="CellReference" />);
///     the writer converts to the 0-based <c>xdr:col</c>/<c>xdr:row</c> the format requires.
/// </remarks>
public sealed class DrawingAnchor
{
    /// <summary>The anchor mode.</summary>
    public DrawingAnchorType AnchorType { get; set; } = DrawingAnchorType.OneCell;

    /// <summary>The top-left cell the drawing is anchored to.</summary>
    public CellReference From { get; set; } = new(1, 1);

    /// <summary>The EMU offset of the top-left corner inside the <see cref="From" /> cell.</summary>
    public Emu FromOffsetX { get; set; } = Emu.Zero;

    /// <summary>The EMU offset of the top-left corner inside the <see cref="From" /> cell.</summary>
    public Emu FromOffsetY { get; set; } = Emu.Zero;

    /// <summary>The bottom-right cell (two-cell anchors only).</summary>
    public CellReference To { get; set; } = new(6, 6);

    /// <summary>The EMU offset of the bottom-right corner inside the <see cref="To" /> cell.</summary>
    public Emu ToOffsetX { get; set; } = Emu.Zero;

    /// <summary>The EMU offset of the bottom-right corner inside the <see cref="To" /> cell.</summary>
    public Emu ToOffsetY { get; set; } = Emu.Zero;

    /// <summary>The drawing width (one-cell and absolute anchors).</summary>
    public Emu Width { get; set; } = Emu.FromPixels(480, 96);

    /// <summary>The drawing height (one-cell and absolute anchors).</summary>
    public Emu Height { get; set; } = Emu.FromPixels(288, 96);

    /// <summary>The absolute X position (absolute anchors only).</summary>
    public Emu OffsetX { get; set; } = Emu.Zero;

    /// <summary>The absolute Y position (absolute anchors only).</summary>
    public Emu OffsetY { get; set; } = Emu.Zero;

    /// <summary>Creates a one-cell anchor pinned at <paramref name="cell" /> with a pixel size.</summary>
    public static DrawingAnchor OneCell(CellReference cell, double widthPixels = 480, double heightPixels = 288) =>
        new()
        {
            AnchorType = DrawingAnchorType.OneCell,
            From = cell,
            Width = Emu.FromPixels(widthPixels, 96),
            Height = Emu.FromPixels(heightPixels, 96)
        };

    /// <summary>Creates a two-cell anchor spanning <paramref name="from" />..<paramref name="to" />.</summary>
    public static DrawingAnchor TwoCell(CellReference from, CellReference to) =>
        new()
        {
            AnchorType = DrawingAnchorType.TwoCell,
            From = from,
            To = to
        };
}
