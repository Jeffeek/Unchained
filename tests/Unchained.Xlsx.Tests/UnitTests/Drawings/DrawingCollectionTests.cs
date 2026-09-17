using Shouldly;
using Unchained.Ooxml.Media;
using Unchained.Xlsx.Drawings;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests.Drawings;

/// <summary>Tests for <see cref="DrawingCollection" /> operations.</summary>
public sealed class DrawingCollectionTests
{
    private static PictureDrawing CreatePicture() =>
        new(new EmbeddedImage("image/png", new byte[] { 0x1, 0x2 }));

    [Fact]
    public void Count_EmptyCollection_ReturnsZero()
    {
        var collection = new DrawingCollection();
        collection.Count.ShouldBe(0);
    }

    [Fact]
    public void Add_AddsDrawing()
    {
        var collection = new DrawingCollection();
        var drawing = CreatePicture();

        collection.Add(drawing);

        collection.Count.ShouldBe(1);
        collection[0].ShouldBeSameAs(drawing);
    }

    [Fact]
    public void Add_NullDrawing_Throws()
    {
        var collection = new DrawingCollection();
        Should.Throw<ArgumentNullException>(() => collection.Add(null!));
    }

    [Fact]
    public void Indexer_ReturnsCorrectDrawing()
    {
        var collection = new DrawingCollection();
        var d1 = CreatePicture();
        var d2 = CreatePicture();
        collection.Add(d1);
        collection.Add(d2);

        collection[0].ShouldBeSameAs(d1);
        collection[1].ShouldBeSameAs(d2);
    }

    [Fact]
    public void Remove_RemovesDrawing()
    {
        var collection = new DrawingCollection();
        var drawing = CreatePicture();
        collection.Add(drawing);

        collection.Remove(drawing);

        collection.Count.ShouldBe(0);
    }

    [Fact]
    public void Pictures_FiltersPictureDrawings()
    {
        var collection = new DrawingCollection();
        var pic = CreatePicture();
        collection.Add(pic);

        collection.Pictures.Count().ShouldBe(1);
        collection.Pictures.First().ShouldBeSameAs(pic);
    }

    [Fact]
    public void GetEnumerator_Generic_EnumeratesDrawings()
    {
        var collection = new DrawingCollection
        {
            CreatePicture(),
            CreatePicture()
        };

        collection.Count.ShouldBe(2);
    }

    [Fact]
    public void GetEnumerator_NonGeneric_EnumeratesDrawings()
    {
        var collection = new DrawingCollection { CreatePicture() };

        collection.Count.ShouldBe(1);
    }

    [Fact]
    public void All_ReturnsInternalList()
    {
        var collection = new DrawingCollection();
        var drawing = CreatePicture();
        collection.Add(drawing);

        var all = collection.All;
        all.Count.ShouldBe(1);
        all[0].ShouldBeSameAs(drawing);
    }
}
