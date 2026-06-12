using System.IO.Packaging;
using Shouldly;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Model-enrichment parity: SDK-loaded documents now preserve the full master/layout/notes XML
///     via RawElement, so a custom-writer save keeps content the typed model does not capture
///     (master txStyles/clrMap, notes-master reference). Previously these were rebuilt minimally.
/// </summary>
public sealed class RawElementPreservationTests
{
    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", name);

    private static string ReadPart(byte[] pptx, string uriContains)
    {
        using var ms = new MemoryStream(pptx);
        using var pkg = Package.Open(ms, FileMode.Open, FileAccess.Read);
        var part = pkg.GetParts().FirstOrDefault(p => p.Uri.ToString().Contains(uriContains));
        if (part is null) return string.Empty;
        using var r = new StreamReader(part.GetStream());
        return r.ReadToEnd();
    }

    [Fact]
    public async Task SdkLoad_CustomSave_PreservesMasterTextStylesAndColorMap()
    {
        var path = SamplePath("mst-slide-layouts.pptx");
        Assert.SkipUnless(File.Exists(path), "sample missing");
        var bytes = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        // Load via SDK (sets master RawElement), save via the CUSTOM writer (default options).
        var doc = await processor.LoadAsync(bytes, new OpenOptions { UseOpenXmlEngine = true });
        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms);
        var saved = ms.ToArray();

        var masterXml = ReadPart(saved, "/slideMasters/slideMaster");
        masterXml.Contains("txStyles").ShouldBeTrue("master text-style hierarchy must survive");
        masterXml.Contains("clrMap").ShouldBeTrue("master colour map must survive");

        doc.Dispose();
    }

    [Fact]
    public async Task Notes_RoundTrip_PreservesFullNotesXml()
    {
        var path = SamplePath("prs-notes.pptx");
        Assert.SkipUnless(File.Exists(path), "sample missing");
        var bytes = await File.ReadAllBytesAsync(path);

        using var processor = new PresentationProcessor();
        var doc = await processor.LoadAsync(bytes, new OpenOptions { UseOpenXmlEngine = true });

        // Capture the original notes text so we can assert it survives a custom-writer save.
        var notedSlide = doc.Slides.FirstOrDefault(static s => !string.IsNullOrWhiteSpace(s.Notes.NotesText));
        notedSlide.ShouldNotBeNull("sample should have at least one slide with notes");
        var originalNotes = notedSlide.Notes.NotesText;

        using var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms);

        var reloaded = await processor.LoadAsync(ms.ToArray(), new OpenOptions { UseOpenXmlEngine = true });
        var reloadedNotes = reloaded.Slides
            .FirstOrDefault(static s => !string.IsNullOrWhiteSpace(s.Notes.NotesText))?.Notes.NotesText;
        reloadedNotes.ShouldBe(originalNotes, "notes text must survive a custom-writer round-trip");

        doc.Dispose();
        reloaded.Dispose();
    }
}
