namespace Unchained.Pdf.Models;

/// <summary>
/// Controls how a PDF page is rasterized to a bitmap (ISO 32000-1 §9).
/// <param name="Dpi">Output resolution in dots per inch. Higher values produce larger, sharper images. Default: 150 (suitable for screen display). Use 300 for print-quality output.</param>
/// <param name="Format">Output file format. Only <see cref="OutputFormat.Png"/> is supported in M5; JPEG requires libjpeg-turbo integration (M5+).</param>
/// </summary>
public sealed record RenderOptions(
    int Dpi = 150,
    OutputFormat Format = OutputFormat.Png
)
{
    // ReSharper disable once MemberCanBeInternal
    public static readonly RenderOptions Default = new();
    public static readonly RenderOptions HighRes = new(Dpi: 300);
}

/// <summary>Output format for rasterized PDF pages.</summary>
public enum OutputFormat
{
    /// <summary>Portable Network Graphics — lossless, pure-managed encoder.</summary>
    Png
}
