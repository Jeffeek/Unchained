using Unchained.Studio.Studio;

namespace Unchained.Studio.Tests.Helpers;

public sealed class PageRangeParserTests
{
    [Fact]
    public void Parse_SinglePage_ReturnsThatPage()
    {
        var result = PageRangeParser.Parse("3", 10, out var error);

        error.ShouldBeNull();
        result.ShouldBe([3]);
    }

    [Fact]
    public void Parse_CommaSeparated_ReturnsSortedDeduplicated()
    {
        var result = PageRangeParser.Parse("3,1,3,5", 10, out var error);

        error.ShouldBeNull();
        result.ShouldBe([1, 3, 5]);
    }

    [Fact]
    public void Parse_Range_ExpandsInclusive()
    {
        var result = PageRangeParser.Parse("5-8", 10, out var error);

        error.ShouldBeNull();
        result.ShouldBe([5, 6, 7, 8]);
    }

    [Fact]
    public void Parse_MixedRangesAndSingles_MergesAndSorts()
    {
        var result = PageRangeParser.Parse("8, 1-3, 5", 10, out var error);

        error.ShouldBeNull();
        result.ShouldBe([1, 2, 3, 5, 8]);
    }

    [Fact]
    public void Parse_OverlappingRanges_Deduplicate()
    {
        var result = PageRangeParser.Parse("1-4,2-6", 10, out var error);

        error.ShouldBeNull();
        result.ShouldBe([1, 2, 3, 4, 5, 6]);
    }

    [
        Theory,
        InlineData(""),
        InlineData("   "),
        InlineData(null)
    ]
    public void Parse_BlankInput_ReturnsNullWithError(string? text)
    {
        var result = PageRangeParser.Parse(text!, 10, out var error);

        result.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_PageAboveMax_ReturnsNullWithError()
    {
        var result = PageRangeParser.Parse("11", 10, out var error);

        result.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_PageBelowOne_ReturnsNullWithError()
    {
        var result = PageRangeParser.Parse("0", 10, out var error);

        result.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_ReversedRange_ReturnsNullWithError()
    {
        var result = PageRangeParser.Parse("8-5", 10, out var error);

        result.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_RangeBeyondMax_ReturnsNullWithError()
    {
        var result = PageRangeParser.Parse("5-20", 10, out var error);

        result.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [
        Theory,
        InlineData("abc"),
        InlineData("1-2-3"),
        InlineData("1-x")
    ]
    public void Parse_MalformedToken_ReturnsNullWithError(string text)
    {
        var result = PageRangeParser.Parse(text, 10, out var error);

        result.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_NegativeNumber_ReturnsNullWithError()
    {
        var result = PageRangeParser.Parse("-1", 10, out var error);

        result.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_LeadingZeros_ParsedNormally()
    {
        var result = PageRangeParser.Parse("007", 10, out var error);

        error.ShouldBeNull();
        result.ShouldBe([7]);
    }
}
