using Shouldly;
using Unchained.Pdf.Core;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Core;

public sealed class PdfDictionaryTests
{
    private static PdfDictionary Dict(params (string Key, PdfObject Value)[] entries)
    {
        var d = new Dictionary<string, PdfObject>();
        foreach (var (k, v) in entries) d[k] = v;
        return new PdfDictionary(d);
    }

    [Fact]
    public void EmptyConstructor_HasNoEntries() =>
        new PdfDictionary().Entries.ShouldBeEmpty();

    [Fact]
    public void StringIndexer_PresentKey_ReturnsValue()
    {
        var dict = Dict(("Type", PdfName.Page));
        dict["Type"].ShouldBeSameAs(PdfName.Page);
    }

    [Fact]
    public void StringIndexer_AbsentKey_ReturnsNull() =>
        new PdfDictionary()["Missing"].ShouldBeNull();

    [Fact]
    public void PdfNameIndexer_PresentKey_ReturnsValue()
    {
        var dict = Dict(("Type", PdfName.Page));
        dict[PdfName.Type].ShouldBeSameAs(PdfName.Page);
    }

    [Fact]
    public void PdfNameIndexer_AbsentKey_ReturnsNull() =>
        new PdfDictionary()[PdfName.Type].ShouldBeNull();

    [Fact]
    public void Get_CorrectType_ReturnsValue()
    {
        var dict = Dict(("Count", new PdfInteger(5)));
        dict.Get<PdfInteger>("Count")!.Value.ShouldBe(5);
    }

    [Fact]
    public void Get_WrongType_ReturnsNull()
    {
        var dict = Dict(("Type", PdfName.Page));
        dict.Get<PdfInteger>("Type").ShouldBeNull();
    }

    [Fact]
    public void Get_AbsentKey_ReturnsNull() =>
        new PdfDictionary().Get<PdfInteger>("Missing").ShouldBeNull();

    [Fact]
    public void Get_ByPdfName_Works()
    {
        var dict = Dict(("Count", new PdfInteger(3)));
        dict.Get<PdfInteger>(PdfName.Count)!.Value.ShouldBe(3);
    }

    [Fact]
    public void TryGet_PresentKey_ReturnsTrueAndValue()
    {
        var dict = Dict(("Type", PdfName.Page));
        var found = dict.TryGet<PdfName>("Type", out var value);
        found.ShouldBeTrue();
        value.ShouldBeSameAs(PdfName.Page);
    }

    [Fact]
    public void TryGet_AbsentKey_ReturnsFalse()
    {
        var found = new PdfDictionary().TryGet<PdfName>("Missing", out _);
        found.ShouldBeFalse();
    }

    [Fact]
    public void TryGet_WrongType_ReturnsFalse()
    {
        var dict = Dict(("Type", PdfName.Page));
        dict.TryGet<PdfInteger>("Type", out _).ShouldBeFalse();
    }

    [Fact]
    public void GetName_PresentNameEntry_ReturnsNameString()
    {
        var dict = Dict(("Type", PdfName.Page));
        dict.GetName("Type").ShouldBe("Page");
    }

    [Fact]
    public void GetName_AbsentKey_ReturnsNull() =>
        new PdfDictionary().GetName("Missing").ShouldBeNull();

    [Fact]
    public void GetName_NonNameValue_ReturnsNull()
    {
        var dict = Dict(("Count", new PdfInteger(1)));
        dict.GetName("Count").ShouldBeNull();
    }

    [Fact]
    public void Entries_ExposesAllPairs()
    {
        var dict = Dict(("A", PdfNull.Instance), ("B", PdfBoolean.True));
        dict.Entries.Count.ShouldBe(2);
        dict.Entries["A"].ShouldBeSameAs(PdfNull.Instance);
        dict.Entries["B"].ShouldBeSameAs(PdfBoolean.True);
    }

    [Fact]
    public void StringIndexer_And_PdfNameIndexer_AreEquivalent()
    {
        var dict = Dict(("Type", PdfName.Page));
        dict["Type"].ShouldBeSameAs(dict[PdfName.Type]);
    }
}
