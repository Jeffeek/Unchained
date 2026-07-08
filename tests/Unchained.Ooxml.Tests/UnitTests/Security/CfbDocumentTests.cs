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

    [Fact]
    public void WriteRead_TwoSmallStreams_BothInMiniStream()
    {
        var data1 = "short"u8.ToArray();
        var data2 = "also short"u8.ToArray();

        var cfb = CfbDocument.Write([("A", data1), ("B", data2)]);
        var streams = CfbDocument.Read(cfb);

        streams.ShouldContainKey("A");
        streams.ShouldContainKey("B");
        streams["A"].ShouldBe(data1);
        streams["B"].ShouldBe(data2);
    }

    [Fact]
    public void WriteRead_SingleStream_RoundTrips()
    {
        var data = "single stream"u8.ToArray();
        var cfb = CfbDocument.Write([("OnlyOne", data)]);
        var streams = CfbDocument.Read(cfb);

        streams.ShouldContainKey("OnlyOne");
        streams["OnlyOne"].ShouldBe(data);
    }

    [Fact]
    public void WriteRead_EmptyStreamsList_RoundsTripWithNoEntries()
    {
        var cfb = CfbDocument.Write([]);
        var streams = CfbDocument.Read(cfb);
        streams.ShouldBeEmpty();
    }

    [Fact]
    public void WriteRead_VeryLargeStream_FullSectorPath()
    {
        // 8 KB — above 4096 cutoff, forces the full-sector code path, but small
        // enough to fit within a single FAT sector (the file size stays under 64 KB).
        var data = new byte[8192];
        new Random(42).NextBytes(data);

        var cfb = CfbDocument.Write([("BigStream", data)]);
        var streams = CfbDocument.Read(cfb);

        streams.ShouldContainKey("BigStream");
        streams["BigStream"].ShouldBe(data);
    }

    [Fact]
    public void WriteRead_StreamNameUnicode_RoundsTrip()
    {
        var data = "unicode"u8.ToArray();
        var cfb = CfbDocument.Write([("données", data)]);
        var streams = CfbDocument.Read(cfb);

        streams.ShouldContainKey("données");
        streams["données"].ShouldBe(data);
    }
}
