using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class JpxDecoderTests
{
    [Fact]
    public async Task Decode_GarbageData_ThrowsInvalidOperationException()
    {
        var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE };
        await Should.ThrowAsync<InvalidOperationException>(() =>
            Task.Run(() => JpxDecoder.Decode(garbage))
        );
    }

    [Fact]
    public async Task Decode_EmptyData_ThrowsInvalidOperationException() =>
        await Should.ThrowAsync<InvalidOperationException>(static () =>
            Task.Run(static () => JpxDecoder.Decode(ReadOnlyMemory<byte>.Empty))
        );
}
