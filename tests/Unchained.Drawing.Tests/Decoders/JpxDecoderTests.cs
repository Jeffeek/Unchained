using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Drawing.Tests.Decoders;

/// <summary>
///     Error-path tests for <see cref="JpxDecoder" />. Malformed JPEG 2000 input is wrapped in an
///     <see cref="InvalidOperationException" /> rather than surfacing the underlying codec failure.
/// </summary>
public sealed class JpxDecoderTests
{
    [Fact]
    public void Decode_GarbageData_ThrowsInvalidOperation() =>
        Should.Throw<InvalidOperationException>(static () =>
            JpxDecoder.Decode(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 })
        );

    [Fact]
    public void Decode_Empty_Throws() =>
        Should.Throw<Exception>(static () => JpxDecoder.Decode(ReadOnlyMemory<byte>.Empty));
}
