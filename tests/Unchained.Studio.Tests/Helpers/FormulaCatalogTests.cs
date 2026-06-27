using Unchained.Studio.Studio.Xlsx;

namespace Unchained.Studio.Tests.Helpers;

public sealed class FormulaCatalogTests
{
    [Fact]
    public void Search_NullPrefix_ReturnsAll()
    {
        var results = FormulaCatalog.Search(null).ToList();
        results.ShouldBe(FormulaCatalog.All);
    }

    [Fact]
    public void Search_EmptyPrefix_ReturnsAll()
    {
        var results = FormulaCatalog.Search("").ToList();
        results.ShouldBe(FormulaCatalog.All);
    }

    [Fact]
    public void Search_PrefixMatch_CaseInsensitive()
    {
        var results = FormulaCatalog.Search("sum").ToList();
        results.ShouldNotBeEmpty();
        results.All(f => f.Name.StartsWith("sum", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }

    [Fact]
    public void Search_PrefixMatch_ExactMatchIncluded()
    {
        var results = FormulaCatalog.Search("SUM").ToList();
        results.ShouldContain(f => f.Name == "SUM");
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var results = FormulaCatalog.Search("zzzznotarealfunction").ToList();
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Search_LeadingEqualsStripped()
    {
        var results = FormulaCatalog.Search("=sum").ToList();
        results.ShouldNotBeEmpty();
        results.ShouldContain(f => f.Name == "SUM");
    }

    [Fact]
    public void Search_OrderedByName()
    {
        var results = FormulaCatalog.Search("A").ToList();
        for (var i = 1; i < results.Count; i++)
            string.Compare(results[i - 1].Name, results[i].Name, StringComparison.OrdinalIgnoreCase)
                .ShouldBeLessThanOrEqualTo(0);
    }
}
