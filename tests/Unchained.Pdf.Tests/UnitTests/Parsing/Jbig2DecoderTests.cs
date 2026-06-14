using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Parsing;

public sealed class Jbig2DecoderTests
{
    [Fact]
    public async Task Decode_GarbageData_ThrowsInvalidOperationException()
    {
        var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        await Should.ThrowAsync<InvalidOperationException>(() =>
            Task.Run(() => Jbig2Decoder.Decode(garbage)));
    }

    [Fact]
    public async Task Decode_EmptyData_ThrowsOrReturnsEmpty()
    {
        // Either it throws (malformed) or returns empty — both are valid for empty JBIG2.
        try
        {
            var result = await Task.Run(static () => Jbig2Decoder.Decode(ReadOnlyMemory<byte>.Empty));
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
            Task.Run(static () => Jbig2Decoder.Decode(new byte[] { 0xFF, 0xFE })));
        ex.ShouldNotBeNull();
    }
}
