using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;

namespace Unchained.Pdf.Models;

/// <summary>
/// A decoded ExtGState soft mask (/SMask), ready for rendering by the page renderer.
/// Carries the mask Form XObject's operators and resource page-adapter so the renderer
/// can render the mask into an off-screen buffer and extract the per-pixel alpha map.
/// ISO 32000-1 §11.6.5.
/// </summary>
/// <param name="WidthPx">Device pixel width of the target page.</param>
/// <param name="HeightPx">Device pixel height of the target page.</param>
/// <param name="MaskType">"Alpha" or "Luminosity" — how to extract opacity from the rendered group.</param>
/// <param name="Operators">Content operators of the mask Form XObject.</param>
/// <param name="FormPage">Resource adapter for the mask form (fonts, images, shadings, etc.).</param>
/// <param name="BBox">Form bounding box [x0, y0, x1, y1] in form user space.</param>
/// <param name="Matrix">Form matrix [a, b, c, d, e, f].</param>
public sealed record SoftMaskInfo(
    int WidthPx,
    int HeightPx,
    string MaskType,
    IReadOnlyList<ContentOperator> Operators,
    IPdfPage FormPage,
    double[] BBox,
    double[] Matrix
);
