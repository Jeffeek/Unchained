using Shouldly;
using Unchained.Drawing.Decoders;
using Xunit;

namespace Unchained.Drawing.Tests.Decoders;

/// <summary>
///     Error-path tests for <see cref="Jbig2Decoder" />. Malformed JBIG2 input is wrapped in an
///     <see cref="InvalidOperationException" />.
/// </summary>
public sealed class Jbig2DecoderTests
{
    [Fact]
    public void Decode_GarbageData_ThrowsInvalidOperation() =>
        Should.Throw<InvalidOperationException>(static () =>
            Jbig2Decoder.Decode(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x11 })
        );

    [Fact]
    public void Decode_WithGlobals_GarbageData_Throws() =>
        Should.Throw<InvalidOperationException>(static () =>
            Jbig2Decoder.Decode(
                new byte[] { 0x01, 0x02, 0x03 },
                new byte[] { 0x04, 0x05, 0x06 }
            )
        );
}
