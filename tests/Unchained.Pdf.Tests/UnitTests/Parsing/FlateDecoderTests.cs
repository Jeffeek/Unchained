using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Drawing.Decoders;
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
        using (var zlib = new ZLibStream(output, CompressionMode.Compress, true))
            zlib.Write(data);
        return output.ToArray();
    }
}
