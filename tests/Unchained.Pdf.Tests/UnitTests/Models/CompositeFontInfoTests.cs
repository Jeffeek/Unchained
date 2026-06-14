using System.Collections.Generic;
using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class CompositeFontInfoTests
{
    [Fact]
    public void IdentityFont_StoresFlagsAndDefaults()
    {
        var info = new CompositeFontInfo(
            IdentityEncoding: true,
            IdentityCidToGid: true,
            CidToGid: null,
            DefaultWidth: 1000,
            Widths: new Dictionary<int, double>());

        info.IdentityEncoding.ShouldBeTrue();
        info.IdentityCidToGid.ShouldBeTrue();
        info.CidToGid.ShouldBeNull();
        info.DefaultWidth.ShouldBe(1000);
        info.Widths.ShouldBeEmpty();
    }

    [Fact]
    public void ExplicitMaps_RoundTrip()
    {
        var cidToGid = new Dictionary<int, int> { [1] = 10, [2] = 20 };
        var widths = new Dictionary<int, double> { [1] = 500, [2] = 750 };
        var info = new CompositeFontInfo(
            IdentityEncoding: false,
            IdentityCidToGid: false,
            CidToGid: cidToGid,
            DefaultWidth: 1000,
            Widths: widths);

        info.IdentityEncoding.ShouldBeFalse();
        info.CidToGid.ShouldNotBeNull();
        info.CidToGid[2].ShouldBe(20);
        info.Widths[1].ShouldBe(500);
    }

    [Fact]
    public void RecordEquality_SameReferences_AreEqual()
    {
        var widths = new Dictionary<int, double> { [0] = 1000 };
        var a = new CompositeFontInfo(true, true, null, 1000, widths);
        var b = new CompositeFontInfo(true, true, null, 1000, widths);
        a.ShouldBe(b);
    }
}
