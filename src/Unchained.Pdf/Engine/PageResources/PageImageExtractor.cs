using Unchained.Drawing.Primitives;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine.PageResources;

/// <summary>
///     Extracts image XObjects from a page's resources and decodes them to packed RGB
///     (3 bytes/pixel), honouring <c>/ColorSpace</c>, <c>/BitsPerComponent</c>, <c>/Decode</c>,
///     Indexed palettes and <c>/SMask</c> soft masks. Extracted from <see cref="PdfPageAdapter" />.
/// </summary>
internal static class PageImageExtractor
{
    internal static IReadOnlyDictionary<string, ImageXObject> GetImageXObjects(PdfDictionary page, PdfDocumentCore core)
    {
        var result = new Dictionary<string, ImageXObject>();
        var resources = core.ResolveDict(page[PdfName.Resources]);
        var xObjDict = core.ResolveDict(resources?[PdfName.XObject]);
        if (xObjDict is null)
            return result;

        foreach (var (key, value) in xObjDict.Entries)
        {
            var stream = core.ResolveStream(value);

            if (stream is null) continue;
            if (stream.Dictionary.GetName(PdfName.Subtype.Value) != "Image") continue;

            var w = (int)(stream.Dictionary.Get<PdfInteger>(PdfName.Width)?.Value ?? 0);
            var h = (int)(stream.Dictionary.Get<PdfInteger>(PdfName.Height)?.Value ?? 0);
            if (w <= 0 || h <= 0) continue;

            var cs = core.ReadColorSpace(stream.Dictionary);
            var bpc = (int)(stream.Dictionary.Get<PdfInteger>(PdfName.BitsPerComponent)?.Value ?? 8);
            var decode = ReadDecodeArray(stream.Dictionary);
            var indexed = ReadIndexedPalette(core, stream.Dictionary);

            byte[] rgb;
            try
            {
                var decoded = StreamFilters.Decode(stream);
                rgb = indexed is not null
                    ? DecodeIndexedToRgb(decoded, w, h, bpc, indexed)
                    : DecodeImageToRgb(
                        decoded,
                        w,
                        h,
                        cs,
                        bpc,
                        decode
                    );
            }
            catch (Exception ex) when (ex is NotImplementedException or NotSupportedException or InvalidOperationException or PdfException)
            {
                // Unsupported filter or malformed data — use gray placeholder.
                rgb = BuildGrayPlaceholder(w, h);
            }

            var alpha = ReadSoftMask(core, stream.Dictionary, w, h);
            result[key] = new ImageXObject(w, h, rgb, alpha);
        }

        return result;
    }

    // Decodes an image's /SMask soft mask into a per-pixel alpha channel (W×H bytes,
    // 0 = transparent, 255 = opaque), resampled to the base image's dimensions. The SMask
    // is a DeviceGray image whose samples are the alpha values (§11.6.5.2). Returns null
    // when there is no soft mask, or it cannot be decoded.
    // ReSharper disable once BadListLineBreaks
    private static byte[]? ReadSoftMask(
        PdfDocumentCore core,
        PdfDictionary imageDict,
        int baseW,
        int baseH
    )
    {
        var smRef = imageDict[PdfName.SMask];
        if (smRef is PdfIndirectReference r)
            smRef = core.ResolveIndirect(r.ObjectNumber).Value;
        if (smRef is not PdfStream smStream)
            return null;

        try
        {
            var smw = (int)(smStream.Dictionary.Get<PdfInteger>(PdfName.Width)?.Value ?? 0);
            var smh = (int)(smStream.Dictionary.Get<PdfInteger>(PdfName.Height)?.Value ?? 0);
            if (smw <= 0 || smh <= 0)
                return null;

            var smBpc = (int)(smStream.Dictionary.Get<PdfInteger>(PdfName.BitsPerComponent)?.Value ?? 8);
            var smDecode = ReadDecodeArray(smStream.Dictionary);
            // Decode as DeviceGray then take one channel per pixel as the alpha value.
            var smRgb = DecodeImageToRgb(
                StreamFilters.Decode(smStream),
                smw,
                smh,
                PdfConstants.DeviceGray,
                smBpc,
                smDecode
            );

            var alpha = new byte[baseW * baseH];
            for (var y = 0; y < baseH; y++)
            for (var x = 0; x < baseW; x++)
            {
                // Nearest-neighbour resample the mask to the base image grid.
                var sx = smw == baseW ? x : x * smw / baseW;
                var sy = smh == baseH ? y : y * smh / baseH;
                alpha[(y * baseW) + x] = smRgb[((sy * smw) + sx) * 3];
            }

            return alpha;
        }
        catch (Exception ex) when (ex is NotImplementedException or NotSupportedException or InvalidOperationException or PdfException)
        {
            return null;
        }
    }

    // Reads the /Decode array as a float[]. Returns null when absent (= identity).
    private static float[]? ReadDecodeArray(PdfDictionary dict)
    {
        if (dict[PdfName.Decode.Value] is not PdfArray da || da.Count == 0)
            return null;

        var values = new float[da.Count];
        for (var i = 0; i < da.Count; i++)
            values[i] = da[i].ReadFloat();

        return values;
    }

    // Detects an [/Indexed base hival lookup] /ColorSpace and parses its palette.
    // Returns null when the colour space is not Indexed.
    private static IndexedPalette? ReadIndexedPalette(PdfDocumentCore core, PdfDictionary dict)
    {
        var csObj = dict[PdfName.ColorSpace.Value];
        if (csObj is PdfIndirectReference r)
            csObj = core.ResolveIndirect(r.ObjectNumber).Value;

        if (csObj is not PdfArray arr || arr.Count < 4)
            return null;
        if ((arr[0] as PdfName)?.Value != PdfConstants.Indexed)
            return null;

        // Base colour space: a name, or a nested array (e.g. [/ICCBased ...]).
        var baseName = core.ResolveBaseSpaceName(arr[1]);
        if (baseName is null)
            return null;

        var baseChannels = baseName switch
        {
            PdfConstants.DeviceGray => 1, PdfConstants.DeviceRgb => 3, PdfConstants.DeviceCmyk => 4, _ => 0
        };
        if (baseChannels == 0)
            return null;

        var hival = (int)((arr[2] as PdfInteger)?.Value ?? 0);
        if (hival < 0)
            return null;

        // Lookup table: a byte string or a stream of (hival+1)*baseChannels bytes.
        var lookupObj = arr[3];
        if (lookupObj is PdfIndirectReference lr)
            lookupObj = core.ResolveIndirect(lr.ObjectNumber).Value;

        var lookup = lookupObj switch
        {
            PdfString s => s.GetBinaryBytes().ToArray(),
            PdfStream st => StreamFilters.Decode(st).ToArray(),
            _ => []
        };
        return lookup.Length == 0 ? null : new IndexedPalette(baseChannels, hival, lookup);
    }

    // Expands an Indexed (paletted) image to packed RGB. Each sample is a palette
    // index (bpc bits) that is looked up in the base-space palette and converted to RGB.
    private static byte[] DecodeIndexedToRgb(
        ReadOnlyMemory<byte> decoded,
        int w,
        int h,
        int bpc,
        IndexedPalette pal
    )
    {
        var pixelCount = w * h;
        var rgb = new byte[pixelCount * 3];
        var data = decoded.Span;
        var lut = pal.Lookup;
        var bc = pal.BaseChannels;
        var rowBytes = ((w * bpc) + 7) / 8;

        for (var row = 0; row < h; row++)
        for (var col = 0; col < w; col++)
        {
            var index = ReadSample(data, row, col, bpc, rowBytes);
            if (index > pal.HiVal) index = pal.HiVal;

            var off = index * bc;
            byte rr, gg, bb;
            if (off + bc > lut.Length)
                rr = gg = bb = 0;
            else
            {
                switch (bc)
                {
                    case 1:
                        rr = gg = bb = lut[off];
                    break;
                    case 3:
                        rr = lut[off];
                        gg = lut[off + 1];
                        bb = lut[off + 2];
                    break;
                    // CMYK base
                    default:
                    {
                        var c = lut[off] / 255.0;
                        var m = lut[off + 1] / 255.0;
                        var y = lut[off + 2] / 255.0;
                        var k = lut[off + 3] / 255.0;
                        var (cr, cg, cb) = ColorMath.CmykToRgb(c, m, y, k);
                        rr = (byte)Math.Clamp(cr * 255, 0, 255);
                        gg = (byte)Math.Clamp(cg * 255, 0, 255);
                        bb = (byte)Math.Clamp(cb * 255, 0, 255);
                        break;
                    }
                }
            }

            var j = ((row * w) + col) * 3;
            rgb[j] = rr;
            rgb[j + 1] = gg;
            rgb[j + 2] = bb;
        }

        return rgb;
    }

    // Reads a single bpc-bit sample (1/2/4/8) at the given row/column from packed image data.
    private static int ReadSample(
        ReadOnlySpan<byte> data,
        int row,
        int col,
        int bpc,
        int rowBytes
    )
    {
        switch (bpc)
        {
            case 8:
            {
                var idx = (row * rowBytes) + col;
                return idx < data.Length ? data[idx] : 0;
            }
            case 1 or 2 or 4:
            {
                var bitPos = col * bpc;
                var byteIdx = (row * rowBytes) + (bitPos >> 3);
                if (byteIdx >= data.Length)
                    return 0;

                var shift = 8 - bpc - (bitPos & 7);
                var mask = (1 << bpc) - 1;

                return (data[byteIdx] >> shift) & mask;
            }
            default:
                return 0;
        }
    }

    // Applies a /Decode array to a single 8-bit sample for one channel.
    // decode[2*ch] = Dmin, decode[2*ch+1] = Dmax per PDF spec §8.9.5.3.
    private static byte ApplyDecode(byte sample, float[]? decode, int channel)
    {
        if (decode is null)
            return sample;

        var idx = channel * 2;
        if (idx + 1 >= decode.Length)
            return sample;

        var dMin = decode[idx];
        var dMax = decode[idx + 1];
        if (Math.Abs(dMax - dMin - 1f) < 1e-4f && Math.Abs(dMin) < 1e-4f)
            return sample; // identity

        var component = dMin + (sample / 255f * (dMax - dMin));
        return (byte)Math.Clamp(component * 255f, 0, 255);
    }

    // Decode raw image bytes into a packed RGB (3 bytes/pixel) array.
    private static byte[] DecodeImageToRgb(
        ReadOnlyMemory<byte> decoded,
        int w,
        int h,
        string? cs,
        int bpc,
        float[]? decode
    )
    {
        var pixelCount = w * h;

        // When cs is null, or the declared colorspace doesn't match the actual decoded
        // channel count (e.g. a CMYK ICC profile but the JPEG decoder collapsed to 1 channel),
        // fall back to the data-length heuristic so we get the best possible rendering
        // rather than a grey placeholder.
        var expectedChannels = cs switch
        {
            PdfConstants.DeviceCmyk => 4, PdfConstants.DeviceRgb => 3, PdfConstants.DeviceGray => 1, _ => 0
        };
        if (bpc == 8 && expectedChannels > 0 && decoded.Length != pixelCount * expectedChannels)
            cs = null; // declared cs doesn't match data; re-infer below

        cs ??= decoded.Length switch
        {
            var n when n == pixelCount * 4 => PdfConstants.DeviceCmyk,
            var n when n == pixelCount * 3 => PdfConstants.DeviceRgb,
            _ => PdfConstants.DeviceGray
        };

        switch (cs)
        {
            // DeviceRGB — direct 3-channel, 8 bpc
            case PdfConstants.DeviceRgb when bpc == 8 && decoded.Length == pixelCount * 3:
            {
                if (decode is null) return decoded.ToArray();

                var span = decoded.Span;
                var rgb = new byte[pixelCount * 3];
                for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
                {
                    rgb[j] = ApplyDecode(span[j], decode, 0);
                    rgb[j + 1] = ApplyDecode(span[j + 1], decode, 1);
                    rgb[j + 2] = ApplyDecode(span[j + 2], decode, 2);
                }

                return rgb;
            }
            // DeviceGray — single channel; replicate to R, G, B
            case PdfConstants.DeviceGray when bpc == 8 && decoded.Length == pixelCount:
            {
                var span = decoded.Span;
                var rgb = new byte[pixelCount * 3];
                for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
                {
                    var v = ApplyDecode(span[i], decode, 0);
                    rgb[j] = rgb[j + 1] = rgb[j + 2] = v;
                }

                return rgb;
            }
            // DeviceCMYK — 4 channels, 8 bpc → convert to RGB
            case PdfConstants.DeviceCmyk when bpc == 8 && decoded.Length == pixelCount * 4:
            {
                var span = decoded.Span;
                var rgb = new byte[pixelCount * 3];
                for (int i = 0, j = 0; i < pixelCount; i++, j += 3)
                {
                    var c = ApplyDecode(span[i * 4], decode, 0) / 255.0;
                    var m = ApplyDecode(span[(i * 4) + 1], decode, 1) / 255.0;
                    var y = ApplyDecode(span[(i * 4) + 2], decode, 2) / 255.0;
                    var k = ApplyDecode(span[(i * 4) + 3], decode, 3) / 255.0;
                    var (cr, cg, cb) = ColorMath.CmykToRgb(c, m, y, k);
                    rgb[j] = (byte)Math.Clamp(cr * 255, 0, 255);
                    rgb[j + 1] = (byte)Math.Clamp(cg * 255, 0, 255);
                    rgb[j + 2] = (byte)Math.Clamp(cb * 255, 0, 255);
                }

                return rgb;
            }
            // DeviceGray 1 bpc (bi-level / CCITTFax) — unpack bit rows to RGB.
            // DeviceGray with the default Decode [0 1]: sample 0 → 0.0 (black), sample 1 → 1.0
            // (white). The CCITTFaxDecode filter already applies its /BlackIs1 flag when
            // producing these samples, so here we only honour the image's own /Decode array.
            // ReSharper disable once InvertIf
            case PdfConstants.DeviceGray when bpc == 1:
            {
                // /Decode default for 1bpc is [0.0 1.0]; [1.0 0.0] inverts black/white.
                var invertBits = decode is { Length: >= 2 } && decode[0] > decode[1];
                var span = decoded.Span;
                var rgb = new byte[pixelCount * 3];
                var rowBytes = (w + 7) / 8;
                for (var row = 0; row < h; row++)
                for (var col = 0; col < w; col++)
                {
                    var byteIdx = (row * rowBytes) + (col >> 3);
                    if (byteIdx >= span.Length) break;

                    var bit = span[byteIdx].BitMsbFirst(col);
                    if (invertBits) bit = 1 - bit;
                    var val = (byte)(bit == 0 ? 0 : 255); // 0=black, 1=white (DeviceGray)
                    var j = ((row * w) + col) * 3;
                    rgb[j] = rgb[j + 1] = rgb[j + 2] = val;
                }

                return rgb;
            }
            default:
                // Unsupported: fall back to grey
                return BuildGrayPlaceholder(w, h);
        }
    }

    private static byte[] BuildGrayPlaceholder(int w, int h)
    {
        var rgb = new byte[w * h * 3];
        Array.Fill(rgb, (byte)128);

        return rgb;
    }

    // An [/Indexed base hival lookup] colour space (§8.6.6.3). BaseChannels is the
    // number of colour components in the base space (1=Gray, 3=RGB, 4=CMYK); Lookup
    // holds (hival+1)*BaseChannels palette bytes, one base-space colour per index.
    private sealed record IndexedPalette(
        int BaseChannels,
        int HiVal,
        byte[] Lookup
    );
}
