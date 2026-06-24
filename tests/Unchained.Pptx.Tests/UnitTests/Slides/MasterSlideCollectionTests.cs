using Shouldly;
using Unchained.Pptx.Slides;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Slides;

public sealed class MasterSlideCollectionTests
{
    [Fact]
    public void Empty_HasZeroCount()
    {
        var collection = new MasterSlideCollection();
        collection.Count.ShouldBe(0);
    }

    [Fact]
    public void Add_AppendsMaster()
    {
        var collection = new MasterSlideCollection();
        var master = new MasterSlide();
        collection.Add(master);
        collection.Count.ShouldBe(1);
        collection[0].ShouldBeSameAs(master);
    }

    [Fact]
    public void Remove_RemovesMaster()
    {
        var collection = new MasterSlideCollection();
        var master = new MasterSlide();
        collection.Add(master);
        collection.Remove(master);
        collection.Count.ShouldBe(0);
    }

    [Fact]
    public void Enumeration_YieldsMastersInOrder()
    {
        var collection = new MasterSlideCollection();
        var a = new MasterSlide { Name = "A" };
        var b = new MasterSlide { Name = "B" };
        collection.Add(a);
        collection.Add(b);
        collection.Select(static m => m.Name).ShouldBe(["A", "B"]);
    }

    [Fact]
    public void NonGenericEnumerator_Works()
    {
        var collection = new MasterSlideCollection { new MasterSlide() };
        var count = collection.Cast<object?>().Count();
        count.ShouldBe(1);
    }

    [Fact]
    public void NonGenericEnumerator_ExplicitInterface_Iterates()
    {
        var collection = new MasterSlideCollection { new MasterSlide() };
        System.Collections.IEnumerable nonGeneric = collection;
        var count = nonGeneric.Cast<object?>().Count();
        count.ShouldBe(1);
    }
}
