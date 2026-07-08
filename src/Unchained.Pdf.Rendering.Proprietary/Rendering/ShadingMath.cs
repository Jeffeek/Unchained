using Unchained.Drawing.Primitives;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Rendering.Proprietary.Rendering;

/// <summary>
///     Pure, state-free gradient mathematics extracted from <see cref="PageRenderer" />:
///     the parametric shading coordinate (axial/radial) and the device→user-space affine
///     inverse used to map each pixel back to shading space. Kept side-effect-free so the
///     correctness-critical gradient math can be reasoned about and tested in isolation,
///     independent of the renderer's mutable graphics state.
/// </summary>
internal static class ShadingMath
{
    // Computes parametric t∈[0,1] for a user-space point under the shading, applying the
    // extend flags. Returns false when the point is outside a non-extended shading.
    internal static bool ShadingT(
        ShadingInfo sh,
        double x,
        double y,
        out double t
    )
    {
        t = 0;
        if (sh.ShadingType == 2)
        {
            // Axial: project (x,y) onto the axis (x0,y0)->(x1,y1).
            var x0 = sh.Coords[0];
            var y0 = sh.Coords[1];
            var x1 = sh.Coords[2];
            var y1 = sh.Coords[3];
            var dx = x1 - x0;
            var dy = y1 - y0;
            var len2 = (dx * dx) + (dy * dy);
            if (len2 < RenderingConstants.DeterminantEpsilon)
            {
                t = 0;
                return true;
            }

            t = (((x - x0) * dx) + ((y - y0) * dy)) / len2;
        }
        else
        {
            // Radial: t such that the point lies on the interpolated circle. Approximate by
            // normalised distance from centre 0 to centre 1 (handles the common concentric case).
            var cx0 = sh.Coords[0];
            var cy0 = sh.Coords[1];
            var r0 = sh.Coords[2];
            var cx1 = sh.Coords[3];
            var cy1 = sh.Coords[4];
            var r1 = sh.Coords[5];
            var d = Vector2D.Distance(x, y, cx1, cy1);
            var denom = r1 - r0;
            t = Math.Abs(denom) > RenderingConstants.DeterminantEpsilon ? (d - r0) / denom : r1 > RenderingConstants.DeterminantEpsilon ? d / r1 : 0;
            _ = cx0;
            _ = cy0;
        }

        if (t < 0)
        {
            if (!sh.ExtendStart) return false;

            t = 0;
        }

        if (t <= 1)
            return true;

        if (!sh.ExtendEnd)
            return false;

        t = 1;

        return true;
    }

    // Inverse of the device→user transform used by PageRenderer.UToPixel (CTM + Y-flip + scale).
    // Returns false when the composed affine is singular.
    internal static bool TryInvertDeviceToUser(
        double[] ctm,
        double scale,
        double pageHeightPt,
        out double[] inv
    )
    {
        // Forward: user (x,y) → ctm → (ux,uy) → device (ux*scale, (H-uy)*scale).
        // Compose forward affine D = [a b c d e f] mapping user→device:
        var a = ctm[0] * scale;
        var b = -ctm[1] * scale;
        var cc = ctm[2] * scale;
        var dd = -ctm[3] * scale;
        var e = ctm[4] * scale;
        var f = (pageHeightPt - ctm[5]) * scale;
        var det = (a * dd) - (b * cc);
        if (Math.Abs(det) < RenderingConstants.MatrixInverseEpsilon)
        {
            inv = [];
            return false;
        }

        var id = 1.0 / det;
        // Inverse affine.
        inv =
        [
            dd * id, -b * id,
            -cc * id, a * id,
            ((cc * f) - (dd * e)) * id, ((b * e) - (a * f)) * id
        ];
        return true;
    }

    internal static (double X, double Y) ApplyInv(IReadOnlyList<double> m, double px, double py) =>
        ((m[0] * px) + (m[2] * py) + m[4], (m[1] * px) + (m[3] * py) + m[5]);
}
