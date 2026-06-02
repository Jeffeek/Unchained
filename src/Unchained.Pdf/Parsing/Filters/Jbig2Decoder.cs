using JBig2Decoder.NETStandard;
using Unchained.Pdf.Core;

namespace Unchained.Pdf.Parsing.Filters;

/// <summary>
/// Decodes JBIG2-compressed data (JBIG2Decode filter, ISO 32000-1 §7.4.7)
/// using JBig2Decoder.NETStandard — a pure-managed C# port of the JPedal JBIG2 decoder.
/// <para>
/// JBIG2 images in PDFs are bi-level (1 bit per pixel, DeviceGray).
/// The decoded output is a raw pixel byte array; image extraction returns a gray
/// placeholder since JBIG2 images are DeviceGray rather than DeviceRGB 8-bit.
/// </para>
/// </summary>
internal static class Jbig2Decoder
{
    /// <summary>
    /// Decodes JBIG2-encoded stream data and returns the raw decoded pixel bytes.
    /// </summary>
    /// <param name="data">The JBIG2-encoded data.</param>
    /// <param name="parms">
    /// Optional /DecodeParms; reads <c>JBIG2Globals</c> (direct <see cref="PdfStream"/> only).
    /// </param>
    internal static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> data, PdfDictionary? parms)
    {
        try
        {
            var decoder = new JBIG2StreamDecoder();

            // /JBIG2Globals: shared segment data that must be set before decoding the image stream.
            // Only direct PdfStream values are supported here; indirect refs require document context.
            if (parms?[PdfName.Get("JBIG2Globals")] is PdfStream globalsStream)
                decoder.SetGlobalData(StreamFilters.Decode(globalsStream).ToArray());

            var result = decoder.DecodeJBIG2(data.ToArray(), out _, out _);

            return result;
        }
        catch (Exception ex) when (ex is not (NotSupportedException or NotImplementedException))
        {
            throw new InvalidOperationException($"JBIG2Decode failed: {ex.Message}", ex);
        }
    }
}
