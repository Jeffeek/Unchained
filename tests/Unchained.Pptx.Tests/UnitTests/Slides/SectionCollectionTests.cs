using System.Collections;
using Shouldly;
using Unchained.Pptx.Slides;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Slides;

/// <summary>
///     Direct unit tests for <see cref="SectionCollection" /> — indexing, both enumerators,
///     name validation, slide-id seeding, and the foreign-section removal guard.
/// </summary>
public sealed class SectionCollectionTests
{
    [Fact]
    public void Add_WithName_AppendsSection()
    {
        var sections = new SectionCollection();
        var s = sections.Add("Intro");
        sections.Count.ShouldBe(1);
        sections[0].ShouldBeSameAs(s);
        s.Name.ShouldBe("Intro");
    }

    [Fact]
    public void Add_WithSlideIds_SeedsSlideIds()
    {
        var sections = new SectionCollection();
        var s = sections.Add("A", [256u, 257u]);
        s.SlideIds.ShouldBe([256u, 257u]);
    }

    [
        Theory,
        InlineData(""),
        InlineData("   ")
    ]
    public void Add_BlankName_Throws(string name)
    {
        var sections = new SectionCollection();
        Should.Throw<ArgumentException>(() => sections.Add(name));
    }

    [Fact]
    public void Remove_ForeignSection_Throws()
    {
        var sections = new SectionCollection();
        var other = new SectionCollection();
        var s = other.Add("Elsewhere");
        Should.Throw<ArgumentException>(() => sections.Remove(s));
    }

    [Fact]
    public void Remove_MemberSection_RemovesIt()
    {
        var sections = new SectionCollection();
        var s = sections.Add("Gone");
        sections.Remove(s);
        sections.Count.ShouldBe(0);
    }

    [Fact]
    public void Clear_EmptiesCollection()
    {
        var sections = new SectionCollection();
        sections.Add("A");
        sections.Add("B");
        sections.Clear();
        sections.Count.ShouldBe(0);
    }

    [Fact]
    public void GenericEnumerator_IteratesSections()
    {
        var sections = new SectionCollection();
        sections.Add("A");
        sections.Add("B");

        var names = sections.Select(static s => s.Name).ToList();
        names.ShouldBe(["A", "B"]);
    }

    [Fact]
    public void NonGenericEnumerator_IteratesSections()
    {
        var sections = new SectionCollection();
        sections.Add("A");

        var count = 0;
        foreach (var _ in (IEnumerable)sections)
            count++;

        count.ShouldBe(1);
    }
}
