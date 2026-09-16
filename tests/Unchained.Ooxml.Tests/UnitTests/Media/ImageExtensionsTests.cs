using Shouldly;
using Unchained.Ooxml.Media;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Media;

/// <summary>
///     Tests for <see cref="ImageExtensions.Extension" /> — maps an image content type to its file
///     extension. Covers every mapped MIME type, both legacy aliases, and the unknown fallback.
/// </summary>
public sealed class ImageExtensionsTests
{
    [
        Theory,
        InlineData("image/png", ".png"),
        InlineData("image/jpeg", ".jpeg"),
        InlineData("image/jpg", ".jpeg"),
        InlineData("image/gif", ".gif"),
        InlineData("image/bmp", ".bmp"),
        InlineData("image/tiff", ".tiff"),
        InlineData("image/svg+xml", ".svg"),
        InlineData("image/emf", ".emf"),
        InlineData("image/x-emf", ".emf"),
        InlineData("image/wmf", ".wmf"),
        InlineData("image/x-wmf", ".wmf")
    ]
    public void Extension_KnownContentType_ReturnsMatchingExtension(string contentType, string expected) =>
        ImageExtensions.Extension(contentType).ShouldBe(expected);

    [
        Theory,
        InlineData("application/octet-stream"),
        InlineData("image/webp"),
        InlineData("IMAGE/PNG"),
        InlineData(""),
        InlineData("png")
    ]
    public void Extension_UnknownContentType_ReturnsBinFallback(string contentType) =>
        ImageExtensions.Extension(contentType).ShouldBe(".bin");
}
