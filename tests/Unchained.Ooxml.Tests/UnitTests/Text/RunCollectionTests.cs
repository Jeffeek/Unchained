using Shouldly;
using Unchained.Ooxml.Text;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Text;

public sealed class RunCollectionTests
{
    [Fact]
    public void Add_AppendsRunWithText()
    {
        var runs = new RunCollection();
        var run = runs.Add("hello");
        run.Text.ShouldBe("hello");
        runs.Count.ShouldBe(1);
        runs[0].ShouldBeSameAs(run);
    }

    [Fact]
    public void Insert_PlacesRunAtIndex()
    {
        var runs = new RunCollection
        {
            "a",
            "c"
        };
        var inserted = runs.Insert(1, "b");
        runs.Count.ShouldBe(3);
        runs[1].ShouldBeSameAs(inserted);
        runs.Select(static r => r.Text).ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void Remove_RemovesRun()
    {
        var runs = new RunCollection();
        var run = runs.Add("x");
        runs.Remove(run);
        runs.Count.ShouldBe(0);
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var runs = new RunCollection
        {
            "a",
            "b"
        };
        runs.Clear();
        runs.Count.ShouldBe(0);
    }

    [Fact]
    public void Enumeration_YieldsRunsInOrder()
    {
        var runs = new RunCollection
        {
            "1",
            "2"
        };
        string.Join(",", runs.Select(static r => r.Text)).ShouldBe("1,2");
    }

    [Fact]
    public void NonGenericEnumerator_Works()
    {
        var runs = new RunCollection { "only" };
        var count = runs.Count;
        count.ShouldBe(1);
    }
}
