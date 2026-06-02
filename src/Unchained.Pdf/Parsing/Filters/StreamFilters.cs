using Unchained.Pdf.Core;

namespace Unchained.Pdf.Parsing.Filters;

/// <summary>
/// Applies the filter chain declared in a stream's /Filter entry
/// (ISO 32000-1 §7.4) to produce the decoded stream data.
/// <para>
/// /Filter may be a single name (<c>/FlateDecode</c>) or an array of names applied
/// left-to-right. /DecodeParms may be a single dictionary or an array parallel to the
/// filter array; each element is passed to its corresponding filter.
/// </para>
/// </summary>
internal static class StreamFilters
{
    /// <summary>
    /// Returns the decoded bytes for <paramref name="stream"/>.
    /// If the stream has no /Filter entry the raw data is returned unchanged.
    /// </summary>
    /// <exception cref="PdfException">Thrown when a filter name is unknown or decoding fails.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown for JBIG2/JPX images with unsupported color spaces.
    /// </exception>
    public static ReadOnlyMemory<byte> Decode(PdfStream stream)
    {
        var filter = stream.Dictionary[PdfName.Filter];
        if (filter is null) return stream.Data;

        var names = filter switch
        {
            PdfName name => (IReadOnlyList<PdfName>)[name],
            PdfArray array => array.Elements.OfType<PdfName>().ToArray(),
            _ => throw new PdfException($"Invalid /Filter value: expected name or array, got {filter.GetType().Name}.")
        };

        // /DecodeParms: single dict (for single filter) or array parallel to /Filter.
        var dpObj = stream.Dictionary[PdfName.Get("DecodeParms")];
        IReadOnlyList<PdfDictionary?> parms = dpObj switch
        {
            PdfDictionary d => [d],
            PdfArray arr => arr.Elements
                .Select(static e => e as PdfDictionary ?? null)
                .ToArray(),
            _ => []
        };

        var data = stream.Data;
        for (var i = 0; i < names.Count; i++)
        {
            var p = i < parms.Count ? parms[i] : null;
            data = ApplyFilter(names[i].Value, data, p);
        }

        return data;
    }

    private static ReadOnlyMemory<byte> ApplyFilter(string name, ReadOnlyMemory<byte> data, PdfDictionary? parms) =>
        name switch
        {
            "FlateDecode" or "Fl" => FlateDecoder.Decode(data),
            "ASCIIHexDecode" or "AHx" => AsciiHexDecoder.Decode(data),
            "ASCII85Decode" or "A85" => Ascii85Decoder.Decode(data),
            "RunLengthDecode" or "RL" => RunLengthDecoder.Decode(data),
            "LZWDecode" or "LZW" => LzwDecoder.Decode(data, parms),
            "CCITTFaxDecode" or "CCF" => CcittFaxDecoder.Decode(data, parms),
            "JBIG2Decode" => Jbig2Decoder.Decode(data, parms),
            "DCTDecode" or "DCT" => JpegDecoder.Decode(data),
            "JPXDecode" => JpxDecoder.Decode(data),
            "Crypt" => data, // identity pass-through
            _ => throw new PdfException($"Unknown stream filter: /{name}.")
        };
}
