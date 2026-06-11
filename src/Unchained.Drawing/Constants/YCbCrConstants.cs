namespace Unchained.Drawing.Constants;

/// <summary>
/// YCbCr ↔ RGB color conversion coefficients.
/// Decode coefficients follow ITU-T T.871 (JFIF); encode coefficients follow ITU-R BT.601.
/// </summary>
internal static class YCbCrConstants
{
    // ── YCbCr → RGB decode (ITU-T T.871) ────────────────────────────────────

    /// <summary>Cr coefficient for the R channel: R = Y + CrToR × (Cr − 128).</summary>
    internal const double CrToR = 1.402;

    /// <summary>Cb coefficient for the G channel: G = Y − CbToGCb × (Cb − 128) − …</summary>
    internal const double CbToGCb = 0.344136;

    /// <summary>Cr coefficient for the G channel: … − CrToGCr × (Cr − 128).</summary>
    internal const double CrToGCr = 0.714136;

    /// <summary>Cb coefficient for the B channel: B = Y + CbToB × (Cb − 128).</summary>
    internal const double CbToB = 1.772;

    // ── RGB → YCbCr encode (ITU-R BT.601) ───────────────────────────────────

    /// <summary>R luma weight: Y = RToY·R + GToY·G + BToY·B − 128.</summary>
    internal const double RToY = 0.299;

    /// <summary>G luma weight.</summary>
    internal const double GToY = 0.587;

    /// <summary>B luma weight.</summary>
    internal const double BToY = 0.114;

    /// <summary>R Cb-chroma weight (applied negated): Cb = −RtoCbNeg·R − GtoCbNeg·G + 0.5·B.</summary>
    internal const double RtoCbNeg = 0.16874;

    /// <summary>G Cb-chroma weight (applied negated).</summary>
    internal const double GtoCbNeg = 0.33126;

    /// <summary>G Cr-chroma weight (applied negated): Cr = 0.5·R − GtoCrNeg·G − BtoCrNeg·B.</summary>
    internal const double GtoCrNeg = 0.41869;

    /// <summary>B Cr-chroma weight (applied negated).</summary>
    internal const double BtoCrNeg = 0.08131;
}
