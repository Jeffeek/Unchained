namespace Unchained.Pptx.Export;

/// <summary>
///     Options that control PPTX-to-PDF conversion.
/// </summary>
public sealed record PdfSaveOptions
{
    /// <summary>A default instance with all settings at their defaults.</summary>
    public static readonly PdfSaveOptions Default = new();

    /// <summary>
    ///     The PDF conformance level to target.
    ///     Default: <see cref="PdfCompliance.Pdf17" />.
    /// </summary>
    public PdfCompliance Compliance { get; init; } = PdfCompliance.Pdf17;

    /// <summary>
    ///     When <see langword="true" />, slides marked as hidden are included in the output.
    ///     Default: <see langword="false" />.
    /// </summary>
    public bool IncludeHiddenSlides { get; init; }

    /// <summary>
    ///     When <see langword="true" />, speaker notes are appended after each slide's page.
    ///     Default: <see langword="false" />.
    /// </summary>
    public bool IncludeNotes { get; init; }

    /// <summary>
    ///     JPEG compression quality (1–100) used when embedding JPEG images.
    ///     Default: <c>85</c>.
    /// </summary>
    public int JpegQuality { get; init; } = 85;

    /// <summary>
    ///     When <see langword="true" />, tagged PDF (PDF/UA) metadata is included.
    ///     Default: <see langword="false" />.
    /// </summary>
    public bool AccessiblePdf { get; init; }

    /// <summary>
    ///     An optional progress callback that receives values in the range [0.0, 1.0].
    /// </summary>
    public IProgress<double>? Progress { get; init; }
}
