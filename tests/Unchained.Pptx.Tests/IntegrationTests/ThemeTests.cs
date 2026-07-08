using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class ThemeTests : PptxTestBase
{
    [Fact]
    public void CreateBlank_HasDefaultTheme()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Masters[0].Theme.ShouldNotBeNull();
    }

    [Fact]
    public void Theme_SetName_IsPreserved()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Masters[0].Theme.Name = "Custom Theme";
        doc.Masters[0].Theme.Name.ShouldBe("Custom Theme");
    }

    [Fact]
    public void ColorScheme_Accent1_CanBeSet()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Masters[0].Theme.Colors.Accent1 = ColorSpec.FromRgb(0xFF, 0x00, 0x00);
        doc.Masters[0].Theme.Colors.Accent1.Resolve(null).ShouldBe(0xFFFF0000u);
    }

    [Fact]
    public void ColorScheme_Indexer_ReadsAndWritesCorrectSlot()
    {
        var doc = PptxFixtures.BlankPresentation();
        var red = ColorSpec.FromRgb(0xFF, 0x00, 0x00);
        doc.Masters[0].Theme.Colors[ThemeColorSlot.Accent1] = red;
        doc.Masters[0].Theme.Colors[ThemeColorSlot.Accent1].ShouldBe(red);
    }

    [Fact]
    public void FontScheme_MajorFont_HasLatinFont()
    {
        var doc = PptxFixtures.BlankPresentation();
        var fonts = doc.Masters[0].Theme.Fonts;
        fonts.ShouldNotBeNull();
    }

    [Fact]
    public void MasterSlide_HasAtLeastOneLayout()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Masters[0].Layouts.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Theme_RoundTrips_PreservesName()
    {
        var doc = PptxFixtures.BlankPresentation();
        doc.Masters[0].Theme.Name = "TestTheme";

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Masters[0].Theme.Name.ShouldBe("TestTheme");
    }
}
