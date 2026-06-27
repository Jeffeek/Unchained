namespace Unchained.Ooxml.Media;

/// <summary>
///     Returns the file extension for an image content type, including the leading dot.
/// </summary>
public static class ImageExtensions
{
    /// <summary>Returns the file extension for <paramref name="contentType" /> (e.g. ".png").</summary>
    public static string Extension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" or "image/jpg" => ".jpeg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "image/tiff" => ".tiff",
        "image/svg+xml" => ".svg",
        "image/x-emf" or "image/emf" => ".emf",
        "image/x-wmf" or "image/wmf" => ".wmf",
        _ => ".bin"
    };
}
