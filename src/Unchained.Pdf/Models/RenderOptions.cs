namespace Unchained.Pdf.Models;

/// <summary>Controls how a PDF page is rasterized to a bitmap (ISO 32000-1 §9).</summary>
/// <param name="Dpi">
/// Output resolution in dots per inch. Higher values produce larger, sharper images.
/// Default: 150 (suitable for screen display). Use 300 for print-quality output.
/// </param>
/// <param name="Format">Output file format. Default is <see cref="OutputFormat.Png"/>.</param>
/// <param name="JpegQuality">
/// JPEG encoding quality from 1 (worst) to 100 (best). Only used when
/// <see cref="Format"/> is <see cref="OutputFormat.Jpeg"/>. Default is 85.
/// </param>
public sealed record RenderOptions(
    int Dpi = 150,
    OutputFormat Format = OutputFormat.Png,
    int JpegQuality = 85
)
{
    /// <summary>Default rendering options: 150 DPI, PNG.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly RenderOptions Default = new();

    /// <summary>High-resolution rendering options: 300 DPI, PNG.</summary>
    public static readonly RenderOptions HighRes = new(Dpi: 300);
}

/// <summary>Output format for rasterized PDF pages.</summary>
public enum OutputFormat
{
    /// <summary>Portable Network Graphics — lossless, pure-managed encoder.</summary>
    Png,

    /// <summary>
    /// JPEG — lossy compression; smaller files. Use <see cref="RenderOptions.JpegQuality"/>
    /// to control the quality/size trade-off (1–100, default 85).
    /// </summary>
    Jpeg,

    /// <summary>Windows Bitmap — uncompressed 24-bit, larger files.</summary>
    Bmp
}
