namespace Unchained.Pdf.Tests.Helpers;

/// <summary>Constants shared across all test classes.</summary>
internal static class PdfTestConstants
{
    /// <summary>The 8-byte PNG file signature (magic bytes).</summary>
    internal static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Extracts the pixel width from a PNG byte array (IHDR bytes 16–19).</summary>
    internal static int PngWidth(byte[] png) =>
        (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];

    /// <summary>Extracts the pixel height from a PNG byte array (IHDR bytes 20–23).</summary>
    internal static int PngHeight(byte[] png) =>
        (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
}
