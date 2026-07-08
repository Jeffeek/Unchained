using Shouldly;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Slides;

/// <summary>
///     Unit coverage for <see cref="SlideCollection" />: add/insert blank and clone, move,
///     remove (by reference and index, including the not-belonging guard), renumbering, and
///     enumeration. Slides are created against a real layout from a blank presentation.
/// </summary>
public sealed class SlideCollectionTests
{
    private static (SlideCollection Slides, SlideLayout Layout) NewDeck()
    {
        var doc = PptxFixtures.BlankPresentation();
        return (doc.Slides, doc.Masters[0].Layouts[0]);
    }

    [Fact]
    public void AddBlank_AppendsAndNumbers()
    {
        var (slides, layout) = NewDeck();
        var a = slides.AddBlank(layout);
        var b = slides.AddBlank(layout);
        slides.Count.ShouldBe(2);
        a.SlideNumber.ShouldBe(1);
        b.SlideNumber.ShouldBe(2);
        a.SlideId.ShouldNotBe(b.SlideId);
    }

    [Fact]
    public void AddBlank_NullLayout_Throws()
    {
        var (slides, _) = NewDeck();
        Should.Throw<ArgumentNullException>(() => slides.AddBlank(null!));
    }

    [Fact]
    public void AddClone_DeepCopiesShapes()
    {
        var (slides, layout) = NewDeck();
        var source = slides.AddBlank(layout);
        source.Name = "Source";

        var clone = slides.AddClone(source);
        clone.Name.ShouldBe("Source");
        clone.SlideId.ShouldNotBe(source.SlideId);
        slides.Count.ShouldBe(2);
    }

    [Fact]
    public void AddClone_NullSource_Throws()
    {
        var (slides, _) = NewDeck();
        Should.Throw<ArgumentNullException>(() => slides.AddClone(null!));
    }

    [Fact]
    public void InsertBlank_PlacesAtIndex()
    {
        var (slides, layout) = NewDeck();
        var first = slides.AddBlank(layout);
        var inserted = slides.InsertBlank(0, layout);
        slides[0].ShouldBeSameAs(inserted);
        slides[1].ShouldBeSameAs(first);
        inserted.SlideNumber.ShouldBe(1);
        first.SlideNumber.ShouldBe(2);
    }

    [Fact]
    public void InsertBlank_NullLayout_Throws()
    {
        var (slides, _) = NewDeck();
        Should.Throw<ArgumentNullException>(() => slides.InsertBlank(0, null!));
    }

    [Fact]
    public void InsertClone_PlacesCloneAtIndex()
    {
        var (slides, layout) = NewDeck();
        var source = slides.AddBlank(layout);
        var clone = slides.InsertClone(0, source);
        slides[0].ShouldBeSameAs(clone);
        slides.Count.ShouldBe(2);
    }

    [Fact]
    public void InsertClone_NullSource_Throws()
    {
        var (slides, _) = NewDeck();
        Should.Throw<ArgumentNullException>(() => slides.InsertClone(0, null!));
    }

    [Fact]
    public void MoveTo_ReordersAndRenumbers()
    {
        var (slides, layout) = NewDeck();
        var a = slides.AddBlank(layout);
        var b = slides.AddBlank(layout);
        slides.MoveTo(0, 1);
        slides[0].ShouldBeSameAs(b);
        slides[1].ShouldBeSameAs(a);
        b.SlideNumber.ShouldBe(1);
    }

    [Fact]
    public void MoveTo_SameIndex_NoOp()
    {
        var (slides, layout) = NewDeck();
        var a = slides.AddBlank(layout);
        slides.MoveTo(0, 0);
        slides[0].ShouldBeSameAs(a);
    }

    [Fact]
    public void Remove_ByReference_Removes()
    {
        var (slides, layout) = NewDeck();
        var a = slides.AddBlank(layout);
        slides.Remove(a);
        slides.Count.ShouldBe(0);
    }

    [Fact]
    public void Remove_NullSlide_Throws()
    {
        var (slides, _) = NewDeck();
        Should.Throw<ArgumentNullException>(() => slides.Remove(null!));
    }

    [Fact]
    public void Remove_SlideNotInCollection_Throws()
    {
        var (slides, _) = NewDeck();
        var (otherSlides, otherLayout) = NewDeck();
        var foreign = otherSlides.AddBlank(otherLayout);
        Should.Throw<ArgumentException>(() => slides.Remove(foreign));
    }

    [Fact]
    public void RemoveAt_RemovesAndRenumbers()
    {
        var (slides, layout) = NewDeck();
        slides.AddBlank(layout);
        var b = slides.AddBlank(layout);
        slides.RemoveAt(0);
        slides.Count.ShouldBe(1);
        slides[0].ShouldBeSameAs(b);
        b.SlideNumber.ShouldBe(1);
    }

    [Fact]
    public void Enumeration_Generic_And_NonGeneric()
    {
        var (slides, layout) = NewDeck();
        slides.AddBlank(layout);
        slides.AddBlank(layout);
        // ReSharper disable once UseCollectionCountProperty
        slides.Count().ShouldBe(2);                 // generic IEnumerator
        slides.Cast<object?>().Count().ShouldBe(2); // non-generic IEnumerator
    }
}
