using System.Text;
using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

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
        await Should.ThrowAsync<InvalidDataException>(() =>
            Task.Run(() => RunLengthDecoder.Decode(input))
        );
    }

    [Fact]
    public async Task Decode_RepeatRunMissingDataByte_ThrowsPdfException()
    {
        // length=200 → repeat run, but no data byte follows
        var input = new byte[] { 200 };
        await Should.ThrowAsync<InvalidDataException>(() =>
            Task.Run(() => RunLengthDecoder.Decode(input))
        );
    }

    [Fact]
    public async Task Decode_EmptyInput_ReturnsEmpty()
    {
        var result = await Task.Run(static () => RunLengthDecoder.Decode(ReadOnlyMemory<byte>.Empty));
        result.Length.ShouldBe(0);
    }
}
