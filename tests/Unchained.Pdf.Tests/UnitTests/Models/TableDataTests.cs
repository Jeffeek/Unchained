using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class TableDataTests
{
    [Fact]
    public void Properties_StoredCorrectly()
    {
        var data = new TableData
        {
            Headers = ["Name", "Age"],
            Rows = [["Alice", "30"], ["Bob", "25"]],
            Title = "People"
        };
        data.Headers.Count.ShouldBe(2);
        data.Rows.Count.ShouldBe(2);
        data.Title.ShouldBe("People");
    }

    [Fact]
    public void Title_NullByDefault()
    {
        var data = new TableData { Headers = [], Rows = [] };
        data.Title.ShouldBeNull();
    }
}
