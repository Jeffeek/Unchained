using Unchained.Pdf.Core;

namespace Unchained.Pdf.Parsing.Filters;

/// <summary>
/// Applies the filter chain declared in a stream's /Filter entry
/// (ISO 32000-1 §7.4) to produce the decoded stream data.
/// <para>
/// /Filter may be a single name (<c>/FlateDecode</c>) or an array of names applied
/// left-to-right. /DecodeParms is not yet supported — all filter parameters use
/// their default values.
/// </para>
/// </summary>
internal static class StreamFilters
{
    /// <summary>
    /// Returns the decoded bytes for <paramref name="stream"/>.
    /// If the stream has no /Filter entry the raw data is returned unchanged.
    /// </summary>
    /// <exception cref="PdfException">
    /// Thrown when a filter name is unknown or when decoding fails.
    /// </exception>
    /// <exception cref="NotImplementedException">
    /// Thrown for filters that are recognized but not yet implemented
    /// (<c>LZWDecode</c>, <c>CCITTFaxDecode</c>, <c>JBIG2Decode</c>, <c>DCTDecode</c>, <c>JPXDecode</c>).
    /// </exception>
    public static ReadOnlyMemory<byte> Decode(PdfStream stream)
    {
        var filter = stream.Dictionary[PdfName.Filter];
        if (filter is null) return stream.Data;

        // /Filter can be a single name or an array of names applied in sequence.
        var names = filter switch
        {
            PdfName name => (IReadOnlyList<PdfName>)[name],
            PdfArray array => array.Elements.OfType<PdfName>().ToArray(),
            _ => throw new PdfException($"Invalid /Filter value: expected name or array, got {filter.GetType().Name}.")
        };

        return names.Aggregate(stream.Data, static (current, name) => ApplyFilter(name.Value, current));
    }

    private static ReadOnlyMemory<byte> ApplyFilter(string filterName, ReadOnlyMemory<byte> data) =>
        filterName switch
        {
            "FlateDecode" or "Fl" => FlateDecoder.Decode(data),
            "ASCIIHexDecode" or "AHx" => AsciiHexDecoder.Decode(data),
            "ASCII85Decode" or "A85" => Ascii85Decoder.Decode(data),
            "RunLengthDecode" or "RL" => RunLengthDecoder.Decode(data),
            "LZWDecode" or "LZW" => throw new NotImplementedException("LZWDecode is not yet implemented."),
            "CCITTFaxDecode" or "CCF" => throw new NotImplementedException("CCITTFaxDecode is not yet implemented."),
            "JBIG2Decode" => throw new NotImplementedException("JBIG2Decode is not yet implemented."),
            "DCTDecode" or "DCT" => throw new NotImplementedException("DCTDecode (JPEG) is not yet implemented."),
            "JPXDecode" => throw new NotImplementedException("JPXDecode (JPEG 2000) is not yet implemented."),
            "Crypt" => data, // identity crypt filter — pass through
            _ => throw new PdfException($"Unknown stream filter: /{filterName}.")
        };
}
