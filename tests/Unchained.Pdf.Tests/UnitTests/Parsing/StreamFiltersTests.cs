using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Drawing;
using Unchained.Pdf.Core;
using Unchained.Pdf.Parsing.Filters;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class FlateDecoderTests
{
    [Fact]
    public void Decode_ValidZlibData_ReturnsOriginalBytes()
    {
        var original = "Hello, Unchained!"u8.ToArray();
        var compressed = Compress(original);

        var result = FlateDecoder.Decode(compressed);

        result.ToArray().ShouldBe(original);
    }

    [Fact]
    public void Decode_EmptyStream_ReturnsEmptyBytes()
    {
        var compressed = Compress([]);
        FlateDecoder.Decode(compressed).Length.ShouldBe(0);
    }

    [Fact]
    public void Decode_CorruptData_ThrowsPdfException()
    {
        var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        Should.Throw<InvalidDataException>(() => FlateDecoder.Decode(garbage));
    }

    [Fact]
    public void Decode_LargePayload_RoundTrips()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("PDF stream data. ", 500)));
        var result = FlateDecoder.Decode(Compress(original));
        result.ToArray().ShouldBe(original);
    }

    private static ReadOnlyMemory<byte> Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionMode.Compress, leaveOpen: true))
            zlib.Write(data);
        return output.ToArray();
    }
}

public sealed class AsciiHexDecoderTests
{
    [
        Theory,
        InlineData("48656C6C6F>", "Hello"),
        InlineData("68 65 6C 6C 6F>", "hello"),
        InlineData(">", "")
    ]
    public void Decode_ValidInput_ReturnsCorrectBytes(string input, string expected)
    {
        var result = AsciiHexDecoder.Decode(Encoding.ASCII.GetBytes(input));
        Encoding.ASCII.GetString(result.Span).ShouldBe(expected);
    }

    [Fact]
    public void Decode_OddNibble_PadsWithZeroOnRight()
    {
        // "4" → 0x40 (padded to "40")
        var result = AsciiHexDecoder.Decode("4>"u8.ToArray());
        result.Span[0].ShouldBe((byte)0x40);
    }

    [Fact]
    public void Decode_LowercaseHex_Works()
    {
        var result = AsciiHexDecoder.Decode("ff>"u8.ToArray());
        result.Span[0].ShouldBe((byte)0xFF);
    }
}

public sealed class Ascii85DecoderTests
{
    [Fact]
    public void Decode_HelloWorld_ReturnsCorrectBytes()
    {
        // "Man" in ASCII85 is "9jqo~>"
        var encoded = "9jqo~>"u8.ToArray();
        var result = Ascii85Decoder.Decode(encoded);
        result.Span[0].ShouldBe((byte)'M');
        result.Span[1].ShouldBe((byte)'a');
        result.Span[2].ShouldBe((byte)'n');
    }

    [Fact]
    public void Decode_ZShorthand_ProducesFourZeroBytes()
    {
        var result = Ascii85Decoder.Decode("z~>"u8.ToArray());
        result.Length.ShouldBe(4);
        result.Span.ToArray().ShouldAllBe(static b => b == 0);
    }

    [Fact]
    public void Decode_EmptyInput_ReturnsEmpty() => Ascii85Decoder.Decode("~>"u8.ToArray()).Length.ShouldBe(0);
}

public sealed class RunLengthDecoderTests
{
    [Fact]
    public void Decode_LiteralRun_CopiesVerbatim()
    {
        // Length=2 → copy 3 bytes: 'A','B','C'; EOD
        var input = new byte[] { 2, (byte)'A', (byte)'B', (byte)'C', 128 };
        var result = RunLengthDecoder.Decode(input);
        Encoding.ASCII.GetString(result.Span).ShouldBe("ABC");
    }

    [Fact]
    public void Decode_RepeatRun_RepeatsCorrectly()
    {
        // Length=253 → 257-253=4 copies of 'X'; EOD
        var input = new byte[] { 253, (byte)'X', 128 };
        var result = RunLengthDecoder.Decode(input);
        result.Length.ShouldBe(4);
        result.Span.ToArray().ShouldAllBe(static b => b == (byte)'X');
    }

    [Fact]
    public void Decode_EodMarker_StopsDecoding()
    {
        var input = new byte[] { 128, 0, (byte)'A' }; // EOD immediately, trailing data ignored
        RunLengthDecoder.Decode(input).Length.ShouldBe(0);
    }
}

public sealed class StreamFiltersTests
{
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
        using (var z = new ZLibStream(ms, CompressionMode.Compress, leaveOpen: true)) z.Write(original);
        var compressed = ms.ToArray();

        var dict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Filter.Value] = PdfName.Get("FlateDecode"),
            [PdfName.Length.Value] = new PdfInteger(compressed.Length)
        });
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
        var dict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Filter.Value] = new PdfArray([
                PdfName.Get("ASCIIHexDecode"),
                PdfName.Get("ASCIIHexDecode")
            ])
        });
        var stream = new PdfStream(dict, doubleEncoded);

        var result = StreamFilters.Decode(stream);
        result.Span[0].ShouldBe((byte)'A');
    }

    [Fact]
    public void Decode_UnknownFilter_ThrowsPdfException() =>
        Should.Throw<PdfException>(static () =>
        {
            var dict = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Filter.Value] = PdfName.Get("UnknownFilter")
            });
            StreamFilters.Decode(new PdfStream(dict, ReadOnlyMemory<byte>.Empty));
        });
}
