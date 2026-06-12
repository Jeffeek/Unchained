using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
/// OpenDocument Presentation (.odp) export and import (M-H): structural round-trip through ODF.
/// </summary>
public sealed class OdpTests : PptxTestBase
{
    private const string OdpMime = "application/vnd.oasis.opendocument.presentation";

    private static byte[] EntryBytes(byte[] zip, string name)
    {
        using var ms = new MemoryStream(zip);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = archive.GetEntry(name);
        if (entry == null) return [];
        using var s = entry.Open();
        using var outMs = new MemoryStream();
        s.CopyTo(outMs);
        return outMs.ToArray();
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportOdp_HasValidPackageStructure()
    {
        var doc = PptxFixtures.WithSlides(2);
        var odp = await Processor.ExportOdpAsync(doc);

        // mimetype must exist, be first, and be stored uncompressed.
        using var ms = new MemoryStream(odp);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var entries = archive.Entries;
        entries[0].FullName.ShouldBe("mimetype");
        entries[0].CompressedLength.ShouldBe(entries[0].Length, "mimetype must be stored, not compressed");

        Encoding.ASCII.GetString(EntryBytes(odp, "mimetype")).ShouldBe(OdpMime);
        archive.GetEntry("content.xml").ShouldNotBeNull();
        archive.GetEntry("styles.xml").ShouldNotBeNull();
        archive.GetEntry("META-INF/manifest.xml").ShouldNotBeNull();
    }

    [Fact]
    public async Task ExportOdp_ContentHasOnePagePerSlide()
    {
        var doc = PptxFixtures.WithSlides(3);
        var odp = await Processor.ExportOdpAsync(doc);
        var content = Encoding.UTF8.GetString(EntryBytes(odp, "content.xml"));

        System.Text.RegularExpressions.Regex.Matches(content, "<draw:page").Count.ShouldBe(3);
    }

    [Fact]
    public async Task ExportOdp_TextAppearsInContent()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1), "OdpHello");

        var odp = await Processor.ExportOdpAsync(doc);
        var content = Encoding.UTF8.GetString(EntryBytes(odp, "content.xml"));
        content.ShouldContain("OdpHello");
    }

    // ── Import ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportOdp_RoundTripsThroughModel()
    {
        var original = PptxFixtures.WithSlides(2);
        original.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1), "Slide one text");

        var odp = await Processor.ExportOdpAsync(original);

        // Load the ODP back through the model (auto-detected by LoadAsync).
        var reloaded = await Processor.LoadAsync(odp);

        reloaded.Slides.Count.ShouldBe(2);
        reloaded.Slides[0].Shapes.OfType<AutoShape>()
            .Any(s => s.TextFrame.PlainText.Contains("Slide one text")).ShouldBeTrue();
    }

    [Fact]
    public async Task ImportOdp_ThenExportPptx_PreservesSlidesAndText()
    {
        var original = PptxFixtures.WithSlides(1);
        original.Slides[0].Shapes.AddTextBox(Emu.Zero, Emu.Zero, Emu.FromInches(4), Emu.FromInches(1), "Cross format");

        var odp = await Processor.ExportOdpAsync(original);
        var fromOdp = await Processor.LoadAsync(odp);

        // Save the ODP-sourced model as PPTX and reload.
        using var pptxMs = new MemoryStream();
        await Processor.SaveAsync(fromOdp, pptxMs);
        var pptx = await Processor.LoadAsync(pptxMs.ToArray());

        pptx.Slides.Count.ShouldBe(1);
        pptx.Slides[0].GetAllText().ShouldContain("Cross format");
    }

    [Fact]
    public async Task ImportOdp_PreservesSlideSize()
    {
        var original = PptxFixtures.WithSlides(1);
        var odp = await Processor.ExportOdpAsync(original);
        var reloaded = await Processor.LoadAsync(odp);

        // Allow a small tolerance for cm rounding on the EMU↔cm round-trip.
        var dw = Math.Abs(reloaded.SlideSize.Width.Value - original.SlideSize.Width.Value);
        var dh = Math.Abs(reloaded.SlideSize.Height.Value - original.SlideSize.Height.Value);
        dw.ShouldBeLessThan(20_000L);
        dh.ShouldBeLessThan(20_000L);
    }

    [Fact]
    public async Task ImportOdp_HiddenSlideFlagRoundTrips()
    {
        var original = PptxFixtures.WithSlides(2);
        original.Slides[1].IsHidden = true;

        var odp = await Processor.ExportOdpAsync(original);
        var reloaded = await Processor.LoadAsync(odp);

        reloaded.Slides[1].IsHidden.ShouldBeTrue();
    }
}
