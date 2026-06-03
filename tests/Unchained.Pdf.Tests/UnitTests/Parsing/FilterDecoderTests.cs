using System.Buffers;
using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Parsing.Filters;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

// ── LzwDecoder ────────────────────────────────────────────────────────────────

public sealed class LzwDecoderTests
{
    /// <summary>
    /// Builds a minimal valid LZW stream that encodes a Clear code followed by
    /// literal byte codes followed by EOD.  Uses 9-bit codes (MSB-first).
    /// </summary>
    private static ReadOnlyMemory<byte> BuildLzwStream(params int[] codes)
    {
        // Pack variable-width codes MSB-first into a byte array.
        // All test streams use 9-bit codes only for simplicity.
        const int width = 9;
        var bits = new List<bool>();
        foreach (var code in codes)
        {
            for (var i = width - 1; i >= 0; i--)
                bits.Add(((code >> i) & 1) == 1);
        }

        // Pad to byte boundary.
        while (bits.Count % 8 != 0) bits.Add(false);

        var bytes = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
                bytes[i / 8] |= (byte)(1 << (7 - (i % 8)));
        }

        return bytes;
    }

    private const int Clear = 256;
    private const int Eod = 257;

    [Fact]
    public async Task Decode_SingleLiteral_ReturnsThatByte()
    {
        // Clear, 'A'(65), EOD
        var data = BuildLzwStream(Clear, 65, Eod);
        var result = await Task.Run(() => LzwDecoder.Decode(data, null));
        result.Length.ShouldBe(1);
        result.Span[0].ShouldBe((byte)'A');
    }

    [Fact]
    public async Task Decode_MultipleLiterals_ReturnsAllBytes()
    {
        // Clear, 'H','e','l','l','o', EOD
        // ReSharper disable BadListLineBreaks
        var data = BuildLzwStream(Clear, 72, 101, 108, 108, 111, Eod);
        // ReSharper restore BadListLineBreaks
        var result = await Task.Run(() => LzwDecoder.Decode(data, null));
        Encoding.ASCII.GetString(result.Span).ShouldBe("Hello");
    }

    [Fact]
    public async Task Decode_EmptyStreamAfterClearAndEod_ReturnsEmpty()
    {
        var data = BuildLzwStream(Clear, Eod);
        var result = await Task.Run(() => LzwDecoder.Decode(data, null));
        result.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Decode_EarlyChangeZero_StillDecodes()
    {
        var parms = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["EarlyChange"] = new PdfInteger(0)
        });
        var data = BuildLzwStream(Clear, 65, Eod);
        var result = await Task.Run(() => LzwDecoder.Decode(data, parms));
        result.Span[0].ShouldBe((byte)'A');
    }

    [Fact]
    public async Task Decode_EarlyChangeOne_ExplicitParm_SameAsDefault()
    {
        var parms = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["EarlyChange"] = new PdfInteger(1)
        });
        var data = BuildLzwStream(Clear, 66, Eod);
        var result = await Task.Run(() => LzwDecoder.Decode(data, parms));
        result.Span[0].ShouldBe((byte)'B');
    }

    [Fact]
    public async Task Decode_MultipleRepeatedSequences_BuildsTableEntries()
    {
        // Clear, 'A','B','A','B' — second 'A' triggers table entry 258='AB',
        // second 'B' may reference 258. Use raw literals to stay below table threshold.
        // ReSharper disable BadListLineBreaks
        var data = BuildLzwStream(Clear, 65, 66, 65, 66, 65, Eod);
        // ReSharper restore BadListLineBreaks
        var result = await Task.Run(() => LzwDecoder.Decode(data, null));
        Encoding.ASCII.GetString(result.Span).ShouldBe("ABABA");
    }

    [Fact]
    public async Task Decode_NullDecodeParms_UsesDefaults()
    {
        var data = BuildLzwStream(Clear, 88, Eod);
        var result = await Task.Run(() => LzwDecoder.Decode(data, null));
        result.Span[0].ShouldBe((byte)'X');
    }

    [Fact]
    public async Task Decode_TruncatedStream_StopsGracefully()
    {
        // A stream that ends mid-code returns -1 from ReadBits → treated as EOD.
        var data = new ReadOnlyMemory<byte>([Clear >> 1]); // 1 byte: only 8 of 9 bits
        var result = await Task.Run(() => LzwDecoder.Decode(data, null));
        result.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Decode_InvalidCodeAboveNextCode_ThrowsPdfException()
    {
        // After Clear, nextCode=258. Sending code 259 (not yet in table) is illegal.
        var data = BuildLzwStream(Clear, 259, Eod);
        await Should.ThrowAsync<PdfException>(() =>
            Task.Run(() => LzwDecoder.Decode(data, null)));
    }
}

// ── RunLengthDecoder (additional edge cases) ─────────────────────────────────

public sealed class RunLengthDecoderAdditionalTests
{
    [Fact]
    public async Task Decode_MaxLiteralRun_127Bytes_CopiesAll()
    {
        // length=127 → copy 128 bytes
        var payload = Enumerable.Range(0, 128).Select(static i => (byte)(i & 0xFF)).ToArray();
        var input = new byte[] { 127 }.Concat(payload).Concat([(byte)128]).ToArray();
        var result = await Task.Run(() => RunLengthDecoder.Decode(input));
        result.Length.ShouldBe(128);
        result.ToArray().ShouldBe(payload);
    }

    [Fact]
    public async Task Decode_MaxRepeatRun_128Copies()
    {
        // length=129 → 257-129=128 copies of byte value
        var input = new byte[] { 129, (byte)'Z', 128 };
        var result = await Task.Run(() => RunLengthDecoder.Decode(input));
        result.Length.ShouldBe(128);
        result.Span.ToArray().ShouldAllBe(static b => b == (byte)'Z');
    }

    [Fact]
    public async Task Decode_ConsecutiveRuns_BothDecoded()
    {
        // literal run: length=0 → 1 byte 'A'
        // repeat run: length=254 → 257-254=3 copies of 'B'
        // EOD
        var input = new byte[] { 0, (byte)'A', 254, (byte)'B', 128 };
        var result = await Task.Run(() => RunLengthDecoder.Decode(input));
        result.Length.ShouldBe(4);
        result.Span[0].ShouldBe((byte)'A');
        result.Span[1].ShouldBe((byte)'B');
        result.Span[2].ShouldBe((byte)'B');
        result.Span[3].ShouldBe((byte)'B');
    }

    [Fact]
    public async Task Decode_LiteralRunTruncated_ThrowsPdfException()
    {
        // length=5 → try to copy 6 bytes, but only 2 follow
        var input = new byte[] { 5, (byte)'A', (byte)'B' };
        await Should.ThrowAsync<PdfException>(() =>
            Task.Run(() => RunLengthDecoder.Decode(input)));
    }

    [Fact]
    public async Task Decode_RepeatRunMissingDataByte_ThrowsPdfException()
    {
        // length=200 → repeat run, but no data byte follows
        var input = new byte[] { 200 };
        await Should.ThrowAsync<PdfException>(() =>
            Task.Run(() => RunLengthDecoder.Decode(input)));
    }

    [Fact]
    public async Task Decode_EmptyInput_ReturnsEmpty()
    {
        var result = await Task.Run(static () => RunLengthDecoder.Decode(ReadOnlyMemory<byte>.Empty));
        result.Length.ShouldBe(0);
    }
}

// ── AsciiHexDecoder (additional edge cases) ───────────────────────────────────

public sealed class AsciiHexDecoderAdditionalTests
{
    [Fact]
    public async Task Decode_MixedCaseHex_ReturnsCorrectBytes()
    {
        // "aB" → 0xAB
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("aB>"u8.ToArray()));
        result.Span[0].ShouldBe((byte)0xAB);
    }

    [Fact]
    public async Task Decode_WhitespaceWithinPairs_Ignored()
    {
        // "4 8 >" → 0x48 = 'H'
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("4 8 >"u8.ToArray()));
        result.Span[0].ShouldBe((byte)0x48);
    }

    [Fact]
    public async Task Decode_TabAndNewlineWhitespace_Ignored()
    {
        // "4\t8\n>" → 0x48
        var input = "4\t8\n>"u8.ToArray();
        var result = await Task.Run(() => AsciiHexDecoder.Decode(input));
        result.Span[0].ShouldBe((byte)0x48);
    }

    [Fact]
    public async Task Decode_AllZeros_ReturnsZeroBytes()
    {
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("0000>"u8.ToArray()));
        result.Length.ShouldBe(2);
        result.Span[0].ShouldBe((byte)0x00);
        result.Span[1].ShouldBe((byte)0x00);
    }

    [Fact]
    public async Task Decode_OddLength_LastNibblePaddedWithZero()
    {
        // "F" → 0xF0
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("F>"u8.ToArray()));
        result.Length.ShouldBe(1);
        result.Span[0].ShouldBe((byte)0xF0);
    }

    [Fact]
    public async Task Decode_InvalidCharacter_ThrowsPdfException() =>
        // 'G' is not valid hex
        await Should.ThrowAsync<PdfException>(static () => Task.Run(static () => AsciiHexDecoder.Decode("GG>"u8.ToArray())));

    [Fact]
    public async Task Decode_MaxByteValue_FF_Works()
    {
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("FF>"u8.ToArray()));
        result.Span[0].ShouldBe((byte)0xFF);
    }

    [Fact]
    public async Task Decode_MultipleBytes_CorrectCount()
    {
        // "48656C6C6F" = "Hello"
        var result = await Task.Run(static () => AsciiHexDecoder.Decode("48656C6C6F>"u8.ToArray()));
        result.Length.ShouldBe(5);
        Encoding.ASCII.GetString(result.Span).ShouldBe("Hello");
    }
}

// ── Ascii85Decoder (additional edge cases) ────────────────────────────────────

public sealed class Ascii85DecoderAdditionalTests
{
    [Fact]
    public async Task Decode_MultipleFullGroups_CorrectOutput()
    {
        // "Man " → base85 group "9jqo", " Lig" → group "F*2M7"
        // Use known encoding: "Man " is encoded as "9jqo" (4 chars → 3 bytes "Man")
        // Actually test two 'z' groups = 8 zero bytes
        var result = await Task.Run(static () => Ascii85Decoder.Decode("zz~>"u8.ToArray()));
        result.Length.ShouldBe(8);
        result.Span.ToArray().ShouldAllBe(static b => b == 0);
    }

    [Fact]
    public async Task Decode_PartialGroup_TwoChars_OneByte()
    {
        // Partial group with 2 chars encodes 1 byte.
        // "!!" in base85 = value 0, emit 1 byte = 0x00
        var result = await Task.Run(static () => Ascii85Decoder.Decode("!!~>"u8.ToArray()));
        result.Length.ShouldBe(1);
        result.Span[0].ShouldBe((byte)0x00);
    }

    [Fact]
    public async Task Decode_PartialGroup_ThreeChars_TwoBytes()
    {
        // Partial group with 3 chars encodes 2 bytes.
        var result = await Task.Run(static () => Ascii85Decoder.Decode("!!!~>"u8.ToArray()));
        result.Length.ShouldBe(2);
    }

    [Fact]
    public async Task Decode_PartialGroup_FourChars_ThreeBytes()
    {
        // Partial group with 4 chars encodes 3 bytes.
        var result = await Task.Run(static () => Ascii85Decoder.Decode("!!!!~>"u8.ToArray()));
        result.Length.ShouldBe(3);
    }

    [Fact]
    public async Task Decode_WhitespaceWithinEncoded_Ignored()
    {
        // "9jqo~>" with internal spaces/newlines still decodes "Man"
        var result = await Task.Run(static () => Ascii85Decoder.Decode("9j qo\n~>"u8.ToArray()));
        result.Length.ShouldBe(3);
        result.Span[0].ShouldBe((byte)'M');
    }

    [Fact]
    public async Task Decode_ZInsideGroup_ThrowsPdfException() =>
        // 'z' appearing when groupLen != 0 is illegal
        await Should.ThrowAsync<PdfException>(static () =>
            Task.Run(static () => Ascii85Decoder.Decode("!z~>"u8.ToArray())));

    [Fact]
    public async Task Decode_TildeNotFollowedByGreaterThan_ThrowsPdfException() =>
        await Should.ThrowAsync<PdfException>(static () =>
            Task.Run(static () => Ascii85Decoder.Decode("~~"u8.ToArray())));

    [Fact]
    public async Task Decode_OutOfRangeCharacter_ThrowsPdfException() =>
        // 'v' (0x76=118) is above 'u' (0x75=117)
        await Should.ThrowAsync<PdfException>(static () =>
            Task.Run(static () => Ascii85Decoder.Decode("vvvvv~>"u8.ToArray())));

    [Fact]
    public async Task Decode_KnownVector_ManEncoding()
    {
        // "9jqo~>" → "Man"
        var result = await Task.Run(static () => Ascii85Decoder.Decode("9jqo~>"u8.ToArray()));
        Encoding.ASCII.GetString(result.Span).ShouldBe("Man");
    }
}

// ── StreamFilters dispatch (additional filter aliases + Crypt pass-through) ──

public sealed class StreamFiltersAdditionalTests
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
        await using (var z = new ZLibStream(ms, CompressionMode.Compress, leaveOpen: true))
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

        var parms = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["EarlyChange"] = new PdfInteger(1)
        });
        var stream = MakeStream("LZWDecode", bytes, parms);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Span[0].ShouldBe((byte)'B');
    }

    [Fact]
    public async Task Decode_InvalidFilterType_ThrowsPdfException()
    {
        // /Filter set to an integer (not a name or array) — should throw.
        var dict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Filter.Value] = new PdfInteger(42)
        });
        var stream = new PdfStream(dict, new byte[] { 1 });
        await Should.ThrowAsync<PdfException>(() =>
            Task.Run(() => StreamFilters.Decode(stream)));
    }

    [Fact]
    public async Task Decode_ArrayFilter_WithDecodeParms_Array_PassesParms()
    {
        // One filter with one DecodeParms dict in an array — Crypt pass-through.
        var data = new byte[] { 9, 8, 7 };
        var dict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Filter.Value] = new PdfArray([PdfName.Get("Crypt")]),
            ["DecodeParms"] = new PdfArray([new PdfDictionary()])
        });
        var stream = new PdfStream(dict, data);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.ToArray().ShouldBe(data);
    }

    [Fact]
    public async Task Decode_ArrayFilter_DecodeParms_NullElement_DoesNotThrow()
    {
        // DecodeParms array with a PdfNull element (treated as null dict).
        var data = new byte[] { 0, (byte)'P', 128 };
        var dict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Filter.Value] = new PdfArray([PdfName.Get("RunLengthDecode")]),
            ["DecodeParms"] = new PdfArray([PdfNull.Instance])
        });
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

        var dict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Filter.Value] = PdfName.Get("LZWDecode"),
            ["DecodeParms"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["EarlyChange"] = new PdfInteger(1)
            })
        });
        var stream = new PdfStream(dict, bytes);
        var result = await Task.Run(() => StreamFilters.Decode(stream));
        result.Span[0].ShouldBe((byte)'C');
    }
}

// ── Jbig2Decoder: minimal smoke tests ────────────────────────────────────────

public sealed class Jbig2DecoderTests
{
    [Fact]
    public async Task Decode_GarbageData_ThrowsInvalidOperationException()
    {
        var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        await Should.ThrowAsync<InvalidOperationException>(() =>
            Task.Run(() => Jbig2Decoder.Decode(garbage, null)));
    }

    [Fact]
    public async Task Decode_EmptyData_ThrowsOrReturnsEmpty()
    {
        // Either it throws (malformed) or returns empty — both are valid for empty JBIG2.
        try
        {
            var result = await Task.Run(static () => Jbig2Decoder.Decode(ReadOnlyMemory<byte>.Empty, null));
            // If it didn't throw: result should at minimum be a non-null memory.
            _ = result.Length; // access property to confirm no NRE
        }
        catch (InvalidOperationException)
        {
            // Expected for malformed/empty data.
        }
    }

    [Fact]
    public async Task Decode_NullDecodeParms_DoesNotThrowNullRef()
    {
        // Should throw InvalidOperationException (bad data), not NullReferenceException.
        var ex = await Should.ThrowAsync<InvalidOperationException>(static () =>
            Task.Run(static () => Jbig2Decoder.Decode(new byte[] { 0xFF, 0xFE }, null)));
        ex.ShouldNotBeNull();
    }
}

// ── JpxDecoder: minimal smoke tests ──────────────────────────────────────────

public sealed class JpxDecoderTests
{
    [Fact]
    public async Task Decode_GarbageData_ThrowsInvalidOperationException()
    {
        var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE };
        await Should.ThrowAsync<InvalidOperationException>(() =>
            Task.Run(() => JpxDecoder.Decode(garbage)));
    }

    [Fact]
    public async Task Decode_EmptyData_ThrowsInvalidOperationException() =>
        await Should.ThrowAsync<InvalidOperationException>(static () =>
            Task.Run(static () => JpxDecoder.Decode(ReadOnlyMemory<byte>.Empty)));
}

// ── ContentStreamWriter ───────────────────────────────────────────────────────

public sealed class ContentStreamWriterTests
{
    private static (ContentStreamWriter writer, ArrayBufferWriter<byte> buffer) CreateWriter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        return (new ContentStreamWriter(buffer), buffer);
    }

    private static string Written(ArrayBufferWriter<byte> buffer) =>
        Encoding.Latin1.GetString(buffer.WrittenSpan);

    [Fact]
    public void Float_WritesFormattedValueWithTrailingSpace()
    {
        var (w, buf) = CreateWriter();
        w.Float(3.14f);
        Written(buf).ShouldBe("3.14 ");
    }

    [Fact]
    public void Float_NegativeValue_WritesMinusSign()
    {
        var (w, buf) = CreateWriter();
        w.Float(-1.5f);
        Written(buf).ShouldBe("-1.5 ");
    }

    [Fact]
    public void Float_ZeroValue_WritesZero()
    {
        var (w, buf) = CreateWriter();
        w.Float(0f);
        Written(buf).ShouldBe("0 ");
    }

    [Fact]
    public void Float_IntegerValue_WritesWithoutDecimalPoint()
    {
        var (w, buf) = CreateWriter();
        w.Float(100f);
        Written(buf).ShouldBe("100 ");
    }

    [Fact]
    public void Int_WritesIntegerWithTrailingSpace()
    {
        var (w, buf) = CreateWriter();
        w.Int(42);
        Written(buf).ShouldBe("42 ");
    }

    [Fact]
    public void Int_NegativeValue_WritesMinusSign()
    {
        var (w, buf) = CreateWriter();
        w.Int(-7);
        Written(buf).ShouldBe("-7 ");
    }

    [Fact]
    public void Int_Zero_WritesZero()
    {
        var (w, buf) = CreateWriter();
        w.Int(0);
        Written(buf).ShouldBe("0 ");
    }

    [Fact]
    public void Name_WritesSlashPrefixedNameWithTrailingSpace()
    {
        var (w, buf) = CreateWriter();
        w.Name("FlateDecode");
        Written(buf).ShouldBe("/FlateDecode ");
    }

    [Fact]
    public void Name_ShortName_CorrectOutput()
    {
        var (w, buf) = CreateWriter();
        w.Name("F1");
        Written(buf).ShouldBe("/F1 ");
    }

    [Fact]
    public void LiteralString_SimpleAscii_WritesParenthesizedWithTrailingSpace()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("Hello");
        Written(buf).ShouldBe("(Hello) ");
    }

    [Fact]
    public void LiteralString_Empty_WritesEmptyParens()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString(string.Empty);
        Written(buf).ShouldBe("() ");
    }

    [Fact]
    public void LiteralString_ContainsOpenParen_Escaped()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("a(b");
        Written(buf).ShouldBe(@"(a\(b) ");
    }

    [Fact]
    public void LiteralString_ContainsCloseParen_Escaped()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("a)b");
        Written(buf).ShouldBe(@"(a\)b) ");
    }

    [Fact]
    public void LiteralString_ContainsBackslash_Escaped()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("a\\b");
        Written(buf).ShouldBe(@"(a\\b) ");
    }

    [Fact]
    public void LiteralString_NonLatin1Char_ReplacedWithQuestionMark()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("Ā"); // code point 256, above Latin-1 range
        Written(buf).ShouldBe("(?) ");
    }

    [Fact]
    public void LiteralString_Latin1MaxChar_Written()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("\xFF"); // 0xFF is within Latin-1
        var output = Written(buf);
        output.ShouldStartWith("(");
        output.ShouldEndWith(") ");
        output[1].ShouldBe('\xFF');
    }

    [Fact]
    public void LiteralString_AllSpecialChars_AllEscaped()
    {
        var (w, buf) = CreateWriter();
        w.LiteralString("()\\");
        Written(buf).ShouldBe(@"(\(\)\\) ");
    }

    [Fact]
    public void Op_WritesKeywordWithNewline()
    {
        var (w, buf) = CreateWriter();
        w.Op("BT"u8);
        Written(buf).ShouldBe("BT\n");
    }

    [Fact]
    public void Op_MultiByteKeyword_WritesCorrectly()
    {
        var (w, buf) = CreateWriter();
        w.Op("Tj"u8);
        Written(buf).ShouldBe("Tj\n");
    }

    [Fact]
    public void MultipleWritesCombined_ProduceCorrectContentStream()
    {
        var (w, buf) = CreateWriter();
        w.Op("BT"u8);
        w.Name("F1");
        w.Int(12);
        w.Op("Tf"u8);
        w.Float(100f);
        w.Float(700f);
        w.Op("Td"u8);
        w.LiteralString("Hello");
        w.Op("Tj"u8);
        w.Op("ET"u8);

        Written(buf).ShouldBe("BT\n/F1 12 Tf\n100 700 Td\n(Hello) Tj\nET\n");
    }

    [Fact]
    public void Float_G6Format_LargeValue_DoesNotUseScientificNotation()
    {
        // G6 with values that fit 6 significant digits should not use 'E' notation
        // for typical PDF coordinate values.
        var (w, buf) = CreateWriter();
        w.Float(595.276f);
        var output = Written(buf);
        output.ShouldNotContain("E");
        output.TrimEnd().Length.ShouldBeGreaterThan(0);
    }
}
