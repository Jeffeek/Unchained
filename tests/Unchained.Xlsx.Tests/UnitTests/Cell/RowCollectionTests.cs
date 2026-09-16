using Shouldly;
using Unchained.Xlsx.Cell;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests.Cell;

/// <summary>Tests for <see cref="RowCollection" /> operations.</summary>
public sealed class RowCollectionTests
{
    [Fact]
    public void Count_EmptyCollection_ReturnsZero()
    {
        var collection = new RowCollection();
        collection.Count.ShouldBe(0);
    }

    [Fact]
    public void GetRow_NotExists_ReturnsNull()
    {
        var collection = new RowCollection();
        collection.GetRow(5).ShouldBeNull();
    }

    [Fact]
    public void GetOrCreateRow_CreatesNewRow()
    {
        var collection = new RowCollection();
        var row = collection.GetOrCreateRow(10);

        row.ShouldNotBeNull();
        row.RowNumber.ShouldBe(10);
        collection.Count.ShouldBe(1);
    }

    [Fact]
    public void GetOrCreateRow_ExistingRow_ReturnsSameInstance()
    {
        var collection = new RowCollection();
        var row1 = collection.GetOrCreateRow(5);
        var row2 = collection.GetOrCreateRow(5);

        row1.ShouldBeSameAs(row2);
        collection.Count.ShouldBe(1);
    }

    [Fact]
    public void GetRow_ExistingRow_ReturnsRow()
    {
        var collection = new RowCollection();
        var created = collection.GetOrCreateRow(3);
        var retrieved = collection.GetRow(3);

        retrieved.ShouldBeSameAs(created);
    }

    [Fact]
    public void Indexer_ReturnsRowAtIndex()
    {
        var collection = new RowCollection();
        collection.GetOrCreateRow(10);
        collection.GetOrCreateRow(20);

        collection[0].RowNumber.ShouldBe(10);
        collection[1].RowNumber.ShouldBe(20);
    }

    [Fact]
    public void GetEnumerator_Generic_EnumeratesRows()
    {
        var collection = new RowCollection();
        collection.GetOrCreateRow(1);
        collection.GetOrCreateRow(2);

        collection.Count().ShouldBe(2);
    }

    [Fact]
    public void GetEnumerator_NonGeneric_EnumeratesRows()
    {
        var collection = new RowCollection();
        collection.GetOrCreateRow(1);

        collection.Cast<object>().Count().ShouldBe(1);
    }

    [Fact]
    public void GetRowsInRange_ReturnsRowsInRange()
    {
        var collection = new RowCollection();
        collection.GetOrCreateRow(5);
        collection.GetOrCreateRow(10);
        collection.GetOrCreateRow(15);
        collection.GetOrCreateRow(20);

        var range = collection.GetRowsInRange(8, 17).ToList();

        range.Count.ShouldBe(2);
        range[0].RowNumber.ShouldBe(10);
        range[1].RowNumber.ShouldBe(15);
    }

    [Fact]
    public void GetRowsInRange_NoRowsInRange_ReturnsEmpty()
    {
        var collection = new RowCollection();
        collection.GetOrCreateRow(5);

        collection.GetRowsInRange(10, 20).ShouldBeEmpty();
    }

    [Fact]
    public void AllRows_ReturnsAllRows()
    {
        var collection = new RowCollection();
        collection.GetOrCreateRow(1);
        collection.GetOrCreateRow(2);

        collection.AllRows.Count().ShouldBe(2);
    }

    [Fact]
    public void Remove_RemovesRow()
    {
        var collection = new RowCollection();
        collection.GetOrCreateRow(10);

        collection.Remove(10);

        collection.Count.ShouldBe(0);
        collection.GetRow(10).ShouldBeNull();
    }

    [Fact]
    public void RenumberFrom_ReplacesAllRows()
    {
        var collection = new RowCollection();
        collection.GetOrCreateRow(1);
        collection.GetOrCreateRow(2);

        var newRows = new[] { new Row(10), new Row(20) };
        collection.RenumberFrom(newRows);

        collection.Count.ShouldBe(2);
        collection.GetRow(1).ShouldBeNull();
        collection.GetRow(10).ShouldNotBeNull();
        collection.GetRow(20).ShouldNotBeNull();
    }

    [Fact]
    public void AddExisting_AddsRow()
    {
        var collection = new RowCollection();
        var row = new Row(5);

        collection.AddExisting(row);

        collection.Count.ShouldBe(1);
        collection.GetRow(5).ShouldBeSameAs(row);
    }
}
