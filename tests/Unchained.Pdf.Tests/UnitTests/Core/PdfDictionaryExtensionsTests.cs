using Shouldly;
using Unchained.Pdf.Core;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Core;

public sealed class PdfDictionaryExtensionsTests
{
    private static PdfDictionary Dict(params (string Key, PdfObject Value)[] entries)
    {
        var d = new Dictionary<string, PdfObject>();
        foreach (var (k, v) in entries) d[k] = v;
        return new PdfDictionary(d);
    }

    [Fact]
    public void IsType_MatchingType_ReturnsTrue() =>
        Dict(("Type", PdfName.Page)).IsType("Page").ShouldBeTrue();

    [Fact]
    public void IsType_DifferentType_ReturnsFalse() =>
        Dict(("Type", PdfName.Page)).IsType("Catalog").ShouldBeFalse();

    [Fact]
    public void IsType_NullDictionary_ReturnsFalse()
    {
        PdfDictionary? dict = null;
        dict.IsType("Page").ShouldBeFalse();
    }

    [Fact]
    public void IsSubtype_Matching_ReturnsTrue() =>
        Dict(("Subtype", PdfName.Get("Image"))).IsSubtype("Image").ShouldBeTrue();

    [Fact]
    public void IsSubtype_Missing_ReturnsFalse() =>
        new PdfDictionary().IsSubtype("Image").ShouldBeFalse();

    [Fact]
    public void IsPage_PageDictionary_ReturnsTrue() =>
        Dict(("Type", PdfName.Page)).IsPage().ShouldBeTrue();

    [Fact]
    public void IsPage_NonPage_ReturnsFalse() =>
        Dict(("Type", PdfName.Catalog)).IsPage().ShouldBeFalse();

    [Fact]
    public void IsCatalog_CatalogDictionary_ReturnsTrue() =>
        Dict(("Type", PdfName.Catalog)).IsCatalog().ShouldBeTrue();

    [Fact]
    public void IsCatalog_NonCatalog_ReturnsFalse() =>
        Dict(("Type", PdfName.Page)).IsCatalog().ShouldBeFalse();
}
