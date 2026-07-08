namespace Unchained.Ooxml.Media;

/// <summary>
///     Returns the file extension for an image content type, including the leading dot.
/// </summary>
public static class ImageExtensions
{
    /// <summary>Returns the file extension for <paramref name="contentType" /> (e.g. ".png").</summary>
    public static string Extension(string contentType) => contentType switch
    {
        MimeTypes.Png => ".png",
        MimeTypes.Jpeg or MimeTypes.Jpg => ".jpeg",
        MimeTypes.Gif => ".gif",
        MimeTypes.Bmp => ".bmp",
        MimeTypes.Tiff => ".tiff",
        MimeTypes.Svg => ".svg",
        MimeTypes.Emf or MimeTypes.EmfLegacy => ".emf",
        MimeTypes.Wmf or MimeTypes.WmfLegacy => ".wmf",
        _ => ".bin"
    };
}
