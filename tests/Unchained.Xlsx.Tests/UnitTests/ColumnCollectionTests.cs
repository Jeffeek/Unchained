using Shouldly;
using Unchained.Xlsx.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

/// <summary>Coverage for the column-range split logic in <c>ColumnCollection</c>.</summary>
public class ColumnCollectionTests
{
    private static ColumnCollection WithWideColumn(int min, int max)
    {
        var collection = new ColumnCollection();
        collection.AddExisting(new Column(min, max) { Width = 20, IsCustomWidth = true, IsHidden = true });
        return collection;
    }

    [Fact]
    public void GetOrCreateColumn_NewColumn_IsAdded()
    {
        var collection = new ColumnCollection();
        var column = collection.GetOrCreateColumn(3);

        column.Min.ShouldBe(3);
        column.Max.ShouldBe(3);
        collection.Count.ShouldBe(1);
    }

    [Fact]
    public void GetOrCreateColumn_ExistingSingle_ReturnsSame()
    {
        var collection = new ColumnCollection();
        var first = collection.GetOrCreateColumn(5);
        var second = collection.GetOrCreateColumn(5);

        second.ShouldBeSameAs(first);
        collection.Count.ShouldBe(1);
    }

    [Fact]
    public void GetOrCreateColumn_SplitsMiddleOfRange()
    {
        var collection = WithWideColumn(2, 6);
        var isolated = collection.GetOrCreateColumn(4);

        isolated.Min.ShouldBe(4);
        isolated.Max.ShouldBe(4);
        isolated.Width.ShouldBe(20);      // inherited
        isolated.IsHidden.ShouldBeTrue(); // inherited

        // Original 2..6 should become 2..3, 4, 5..6.
        collection.Count.ShouldBe(3);
        collection.GetColumn(2)!.Max.ShouldBe(3);
        collection.GetColumn(6)!.Min.ShouldBe(5);
        collection.GetColumn(4).ShouldBeSameAs(isolated);
    }

    [Fact]
    public void GetOrCreateColumn_SplitsLeftEdge()
    {
        var collection = WithWideColumn(2, 6);
        var isolated = collection.GetOrCreateColumn(2);

        isolated.Min.ShouldBe(2);
        isolated.Max.ShouldBe(2);
        // Only a right remainder 3..6 plus the isolated column.
        collection.Count.ShouldBe(2);
        collection.GetColumn(6)!.Min.ShouldBe(3);
    }

    [Fact]
    public void GetOrCreateColumn_SplitsRightEdge()
    {
        var collection = WithWideColumn(2, 6);
        var isolated = collection.GetOrCreateColumn(6);

        isolated.Min.ShouldBe(6);
        isolated.Max.ShouldBe(6);
        collection.Count.ShouldBe(2);
        collection.GetColumn(2)!.Max.ShouldBe(5);
    }

    [Fact]
    public void GetColumn_OutsideRange_ReturnsNull()
    {
        var collection = WithWideColumn(2, 6);
        collection.GetColumn(99).ShouldBeNull();
    }

    [Fact]
    public void Indexer_And_Enumeration()
    {
        var collection = new ColumnCollection();
        collection.GetOrCreateColumn(1);
        collection.GetOrCreateColumn(2);

        collection[0].ShouldNotBeNull();
        collection.Count.ShouldBe(2);
        collection.Select(static c => c.Min).Count().ShouldBe(2);
    }

    [Fact]
    public void Ordered_SortsByMin()
    {
        var collection = new ColumnCollection();
        collection.AddExisting(new Column(5, 5));
        collection.AddExisting(new Column(1, 1));

        collection.Ordered.Select(static c => c.Min).ShouldBe([1, 5]);
    }
}
