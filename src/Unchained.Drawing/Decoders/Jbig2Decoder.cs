using JBig2Decoder.NETStandard;

namespace Unchained.Drawing.Decoders;

/// <summary>
/// Decodes JBIG2-compressed data using JBig2Decoder.NETStandard.
/// Used by PDF /JBIG2Decode (ISO 32000-1 §7.4.7).
/// JBIG2 images are bi-level (1 bit per pixel, DeviceGray).
/// </summary>
internal static class Jbig2Decoder
{
    /// <param name="data">The JBIG2-encoded image stream bytes.</param>
    /// <param name="globals">
    /// Optional decoded bytes of the JBIG2Globals shared segment data.
    /// Must be provided when the stream references shared segments.
    /// </param>
    internal static ReadOnlyMemory<byte> Decode(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte>? globals = null)
    {
        try
        {
            var decoder = new JBIG2StreamDecoder();

            if (globals is { Length: > 0 } g)
                decoder.SetGlobalData(g.ToArray());

            return decoder.DecodeJBIG2(data.ToArray(), out _, out _);
        }
        catch (Exception ex) when (ex is not (NotSupportedException or NotImplementedException))
        {
            throw new InvalidOperationException($"JBIG2Decode failed: {ex.Message}", ex);
        }
    }
}
