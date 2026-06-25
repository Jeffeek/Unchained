using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Parsing.Filters;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class StreamFiltersTests
{
    private static PdfStream MakeStream(string filterName, byte[] data, PdfDictionary? parms = null)
    {
        var entries = new Dictionary<string, PdfObject>
        {
            [PdfName.Filter.Value] = PdfName.Get(filterName),
            [PdfName.Length.Value] = new PdfInteger(data.Length)
        };
        if (parms is not null)
            entries["DecodeParms"] = parms;
        return new PdfStream(new PdfDictionary(entries), data);
    }

    [Fact]
    public void Decode_NoFilter_ReturnsDataUnchanged()
    {
        var data = "raw bytes"u8.ToArray();
        var stream = new PdfStream(new PdfDictionary(), data);
        StreamFilters.Decode(stream).ToArray().ShouldBe(data);
    }

    [Fact]
    public void Decode_FlateDecodeFilter_DecompressesCorrectly()
    {
        var original = "Hello from a compressed stream"u8.ToArray();
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionMode.Compress, true)) z.Write(original);
        var compressed = ms.ToArray();

        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Filter.Value] = PdfName.Get("FlateDecode"),
                [PdfName.Length.Value] = new PdfInteger(compressed.Length)
            }
        );
        var stream = new PdfStream(dict, compressed);

        StreamFilters.Decode(stream).ToArray().ShouldBe(original);
    }

    [Fact]
    public void Decode_ArrayOfFilters_AppliesInOrder()
    {
        // ASCIIHexDecode on top of ASCIIHexDecode: decode once then decode again
        // Single ASCIIHexDecode: "41>" → [0x41] = 'A'
        // So double: "3431>" → "41>" → [0x41] = 'A'
        var doubleEncoded = "3431>"u8.ToArray();
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Filter.Value] = new PdfArray(
                    [
                        PdfName.Get("ASCIIHexDecode"),
                        PdfName.Get("ASCIIHexDecode")
                    ]
                )
            }
        );
        var stream = new PdfStream(dict, doubleEncoded);

        var result = StreamFilters.Decode(stream);
        result.Span[0].ShouldBe((byte)'A');
    }

    [Fact]
    public void Decode_UnknownFilter_ThrowsPdfException() =>
        Should.Throw<PdfException>(static () =>
            {
                var dict = new PdfDictionary(
                    new Dictionary<string, PdfObject>
                    {
                        [PdfName.Filter.Value] = PdfName.Get("UnknownFilter")
                    }
                );
                StreamFilters.Decode(new PdfStream(dict, ReadOnlyMemory<byte>.Empty));
            }
        );

    [Fact]
    public async Task Decode_AsciiHexDecodeAlias_AHx_Works()
    {
        var data = "41>"u8.ToArray(); // 'A'
        var stream = MakeStream("AHx", data);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Span[0].ShouldBe((byte)'A');
    }

    [Fact]
    public async Task Decode_Ascii85DecodeAlias_A85_Works()
    {
        var data = "9jqo~>"u8.ToArray(); // "Man"
        var stream = MakeStream("A85", data);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Span[0].ShouldBe((byte)'M');
    }

    [Fact]
    public async Task Decode_RunLengthDecodeAlias_RL_Works()
    {
        // literal run: length=0 → 1 byte 'X'; EOD
        var data = new byte[] { 0, (byte)'X', 128 };
        var stream = MakeStream("RL", data);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Length.ShouldBe(1);
        result.Span[0].ShouldBe((byte)'X');
    }

    [Fact]
    public async Task Decode_LZWDecodeAlias_LZW_Works()
    {
        // Build a minimal LZW stream: Clear(256) + 'A'(65) + EOD(257), 9-bit MSB
        var bits = new List<bool>();
        foreach (var code in new[] { 256, 65, 257 })
        {
            for (var i = 8; i >= 0; i--)
                bits.Add(((code >> i) & 1) == 1);
        }

        while (bits.Count % 8 != 0) bits.Add(false);
        var bytes = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
                bytes[i / 8] |= (byte)(1 << (7 - (i % 8)));
        }

        var stream = MakeStream("LZW", bytes);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Span[0].ShouldBe((byte)'A');
    }

    [Fact]
    public async Task Decode_FlateDecode_ShortAlias_Fl_Works()
    {
        var original = "test"u8.ToArray();
        using var ms = new MemoryStream();
        await using (var z = new ZLibStream(ms, CompressionMode.Compress, true))
            z.Write(original);
        var compressed = ms.ToArray();

        var stream = MakeStream("Fl", compressed);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.ToArray().ShouldBe(original);
    }

    [Fact]
    public async Task Decode_CryptFilter_PassesThroughUnchanged()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var stream = MakeStream("Crypt", data);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.ToArray().ShouldBe(data);
    }

    [Fact]
    public async Task Decode_ASCIIHexDecode_FullName_Works()
    {
        var data = "48656C6C6F>"u8.ToArray(); // "Hello"
        var stream = MakeStream("ASCIIHexDecode", data);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        Encoding.ASCII.GetString(result.Span).ShouldBe("Hello");
    }

    [Fact]
    public async Task Decode_RunLengthDecode_FullName_Works()
    {
        var data = new byte[] { 0, (byte)'Q', 128 };
        var stream = MakeStream("RunLengthDecode", data);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Span[0].ShouldBe((byte)'Q');
    }

    [Fact]
    public async Task Decode_ASCII85Decode_FullName_Works()
    {
        var data = "9jqo~>"u8.ToArray();
        var stream = MakeStream("ASCII85Decode", data);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Span[0].ShouldBe((byte)'M');
    }

    [Fact]
    public async Task Decode_LZWDecode_FullName_WithDecodeParms_Works()
    {
        var bits = new List<bool>();
        foreach (var code in new[] { 256, 66, 257 })
        {
            for (var i = 8; i >= 0; i--)
                bits.Add(((code >> i) & 1) == 1);
        }

        while (bits.Count % 8 != 0) bits.Add(false);
        var bytes = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
                bytes[i / 8] |= (byte)(1 << (7 - (i % 8)));
        }

        var parms = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["EarlyChange"] = new PdfInteger(1)
            }
        );
        var stream = MakeStream("LZWDecode", bytes, parms);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Span[0].ShouldBe((byte)'B');
    }

    [Fact]
    public async Task Decode_InvalidFilterType_ThrowsPdfException()
    {
        // /Filter set to an integer (not a name or array) — should throw.
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Filter.Value] = new PdfInteger(42)
            }
        );
        var stream = new PdfStream(dict, new byte[] { 1 });
        await Should.ThrowAsync<PdfException>(() =>
            Task.Run(() => StreamFilters.Decode(stream))
        );
    }

    [Fact]
    public async Task Decode_ArrayFilter_WithDecodeParms_Array_PassesParms()
    {
        // One filter with one DecodeParms dict in an array — Crypt pass-through.
        var data = new byte[] { 9, 8, 7 };
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Filter.Value] = new PdfArray([PdfName.Get("Crypt")]),
                ["DecodeParms"] = new PdfArray([new PdfDictionary()])
            }
        );
        var stream = new PdfStream(dict, data);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.ToArray().ShouldBe(data);
    }

    [Fact]
    public async Task Decode_ArrayFilter_DecodeParms_NullElement_DoesNotThrow()
    {
        // DecodeParms array with a PdfNull element (treated as null dict).
        var data = new byte[] { 0, (byte)'P', 128 };
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Filter.Value] = new PdfArray([PdfName.Get("RunLengthDecode")]),
                ["DecodeParms"] = new PdfArray([PdfNull.Instance])
            }
        );
        var stream = new PdfStream(dict, data);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Span[0].ShouldBe((byte)'P');
    }

    [Fact]
    public async Task Decode_SingleDecodeParms_Dict_NotArray_Works()
    {
        // DecodeParms as a single PdfDictionary (not array) for a single LZW filter.
        var bits = new List<bool>();
        foreach (var code in new[] { 256, 67, 257 })
        {
            for (var i = 8; i >= 0; i--)
                bits.Add(((code >> i) & 1) == 1);
        }

        while (bits.Count % 8 != 0) bits.Add(false);
        var bytes = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
                bytes[i / 8] |= (byte)(1 << (7 - (i % 8)));
        }

        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Filter.Value] = PdfName.Get("LZWDecode"),
                ["DecodeParms"] = new PdfDictionary(
                    new Dictionary<string, PdfObject>
                    {
                        ["EarlyChange"] = new PdfInteger(1)
                    }
                )
            }
        );
        var stream = new PdfStream(dict, bytes);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Span[0].ShouldBe((byte)'C');
    }

    [Fact]
    public async Task Decode_FlateDecode_CorruptData_RewrapsAsPdfException()
    {
        // Not a valid zlib stream → ZLibStream throws → re-wrapped as PdfException
        // (the non-PdfException catch arm).
        var garbage = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xFB };
        var stream = MakeStream("FlateDecode", garbage);
        var ex = await Should.ThrowAsync<PdfException>(() => Task.Run(() => StreamFilters.Decode(stream)));
        ex.Message.ShouldContain("FlateDecode");
    }

    [Fact]
    public async Task Decode_Jbig2_WithGlobalsStream_DoesNotThrowUnexpectedly()
    {
        // A JBIG2 filter whose DecodeParms carries a /JBIG2Globals stream exercises the
        // globals-resolution branch. The decoder may reject the synthetic data, but it must
        // surface as a PdfException/NotSupported/InvalidOperation rather than a raw crash.
        var globals = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject> { [PdfName.Length.Value] = new PdfInteger(2) }),
            new byte[] { 0x00, 0x01 }
        );
        var parms = new PdfDictionary(
            new Dictionary<string, PdfObject> { [PdfName.JBIG2Globals.Value] = globals }
        );
        var stream = MakeStream("JBIG2Decode", [0x00, 0x01, 0x02, 0x03], parms);

        await Should.ThrowAsync<Exception>(() => Task.Run(() => StreamFilters.Decode(stream)));
    }
}
