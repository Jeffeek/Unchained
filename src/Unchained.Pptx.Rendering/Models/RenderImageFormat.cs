namespace Unchained.Pptx.Rendering;

/// <summary>The image format used when encoding a rendered slide.</summary>
public enum RenderImageFormat
{
    /// <summary>Portable Network Graphics — lossless, recommended for most uses.</summary>
    Png,

    /// <summary>JPEG — lossy compression; smaller files at the cost of quality.</summary>
    Jpeg,

    /// <summary>Windows Bitmap — uncompressed, large files.</summary>
    Bmp
}
