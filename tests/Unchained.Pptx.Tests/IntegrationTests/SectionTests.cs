using Shouldly;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class SectionTests : PptxTestBase
{
    // ── Default state ─────────────────────────────────────────────────────────

    [Fact]
    public void Sections_Empty_ByDefault()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Sections.Count.ShouldBe(0);
    }

    // ── SectionCollection ─────────────────────────────────────────────────────

    [Fact]
    public void Sections_Add_StoresSection()
    {
        var doc = PptxFixtures.WithSlides(2);
        var section = doc.Sections.Add("Introduction");
        section.Name.ShouldBe("Introduction");
        doc.Sections.Count.ShouldBe(1);
    }

    [Fact]
    public void Sections_Add_WithSlideIds_StoresIds()
    {
        var doc = PptxFixtures.WithSlides(2);
        var id1 = doc.Slides[0].SlideId;
        var id2 = doc.Slides[1].SlideId;

        var section = doc.Sections.Add("Section A", [id1, id2]);

        section.SlideIds.Count.ShouldBe(2);
        section.SlideIds[0].ShouldBe(id1);
        section.SlideIds[1].ShouldBe(id2);
    }

    [Fact]
    public void Sections_Remove_DecreasesCount()
    {
        var doc = PptxFixtures.WithSlides(1);
        var section = doc.Sections.Add("To Remove");
        doc.Sections.Remove(section);
        doc.Sections.Count.ShouldBe(0);
    }

    [Fact]
    public void Sections_Clear_RemovesAll()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Sections.Add("A");
        doc.Sections.Add("B");
        doc.Sections.Clear();
        doc.Sections.Count.ShouldBe(0);
    }

    [Fact]
    public void Sections_MultipleAdd_AllStored()
    {
        var doc = PptxFixtures.WithSlides(4);
        doc.Sections.Add("Part 1", [doc.Slides[0].SlideId, doc.Slides[1].SlideId]);
        doc.Sections.Add("Part 2", [doc.Slides[2].SlideId, doc.Slides[3].SlideId]);

        doc.Sections.Count.ShouldBe(2);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RoundTrip_SingleSection_NamePreserved()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Sections.Add("Introduction", [doc.Slides[0].SlideId]);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Sections.Count.ShouldBe(1);
        reloaded.Sections[0].Name.ShouldBe("Introduction");
    }

    [Fact]
    public async Task RoundTrip_SingleSection_SlideIdsPreserved()
    {
        var doc = PptxFixtures.WithSlides(2);
        var id = doc.Slides[0].SlideId;
        doc.Sections.Add("Section 1", [id]);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Sections[0].SlideIds.ShouldContain(id);
    }

    [Fact]
    public async Task RoundTrip_MultipleSections_AllPreserved()
    {
        var doc = PptxFixtures.WithSlides(4);
        doc.Sections.Add("Chapter 1", [doc.Slides[0].SlideId, doc.Slides[1].SlideId]);
        doc.Sections.Add("Chapter 2", [doc.Slides[2].SlideId, doc.Slides[3].SlideId]);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Sections.Count.ShouldBe(2);
        reloaded.Sections[0].Name.ShouldBe("Chapter 1");
        reloaded.Sections[1].Name.ShouldBe("Chapter 2");
    }

    [Fact]
    public async Task RoundTrip_NoSections_SlideCountUnchanged()
    {
        var doc = PptxFixtures.WithSlides(3);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Slides.Count.ShouldBe(3);
        reloaded.Sections.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RoundTrip_SectionWithAllSlides_Preserved()
    {
        var doc = PptxFixtures.WithSlides(3);
        var ids = Enumerable.Range(0, doc.Slides.Count)
                            .Select(i => doc.Slides[i].SlideId)
                            .ToList();
        doc.Sections.Add("Full Deck", ids);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);

        reloaded.Sections[0].SlideIds.Count.ShouldBe(3);
    }
}
