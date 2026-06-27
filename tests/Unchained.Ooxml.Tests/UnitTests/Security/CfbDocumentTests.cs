using Shouldly;
using Unchained.Ooxml.Security;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Security;

/// <summary>
///     Unit tests for <see cref="CfbDocument" /> — the minimal OLE Compound File Binary
///     reader/writer used by the OOXML encryption path. Exercises the class in isolation,
///     with no presentation document or processor involved.
/// </summary>
public sealed class CfbDocumentTests
{
    [Fact]
    public void WriteRead_SmallAndLargeStreams_RoundTrip()
    {
        var smallData = "<?xml version=\"1.0\"?><root>hello world</root>"u8.ToArray();
        var largeData = new byte[8192];
        new Random(42).NextBytes(largeData);

        var cfb = CfbDocument.Write(
            [
                ("EncryptionInfo", smallData),
                ("EncryptedPackage", largeData)
            ]
        );

        var streams = CfbDocument.Read(cfb);

        streams.ShouldContainKey("EncryptionInfo");
        streams.ShouldContainKey("EncryptedPackage");
        streams["EncryptionInfo"].ShouldBe(smallData);
        streams["EncryptedPackage"].ShouldBe(largeData);
    }
}
