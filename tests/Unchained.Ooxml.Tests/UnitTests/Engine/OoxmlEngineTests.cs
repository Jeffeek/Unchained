using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using Shouldly;
using Unchained.Ooxml.Engine;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Engine;

public sealed class OoxmlEngineTests
{
    [
        Theory,
        InlineData(OoxmlFormat.Presentation),
        InlineData(OoxmlFormat.Wordprocessing),
        InlineData(OoxmlFormat.Spreadsheet)
    ]
    public void Create_ProducesPackageOfRequestedFormat(OoxmlFormat format)
    {
        using var engine = OoxmlEngine.Create(format);
        engine.Format.ShouldBe(format);
        engine.Package.ShouldNotBeNull();
    }

    [Fact]
    public void CreatePresentation_AddMainPart_SaveAndReopen_RoundTripsFormat()
    {
        byte[] bytes;
        using (var engine = OoxmlEngine.Create(OoxmlFormat.Presentation))
        {
            // A freshly created presentation needs a presentation part with a minimal body
            // before the content-type map names it as a presentation.
            var doc = (PresentationDocument)engine.Package;
            var presPart = doc.AddPresentationPart();
            presPart.Presentation = new Presentation(
                new SlideIdList(),
                new SlideSize { Cx = 9144000, Cy = 6858000 },
                new NotesSize { Cx = 6858000, Cy = 9144000 });
            presPart.Presentation.Save();

            bytes = engine.Save();
        }

        bytes.Length.ShouldBeGreaterThan(0);

        // Reopen the saved bytes through the engine — format must be detected as Presentation.
        using var reopened = OoxmlEngine.Open(bytes, false);
        reopened.Format.ShouldBe(OoxmlFormat.Presentation);
    }

    [Fact]
    public void Open_GarbageBytes_Throws() =>
        Should.Throw<Exception>(() => OoxmlEngine.Open([1, 2, 3, 4], false));

    [Fact]
    public void Save_LeavesEngineUsable()
    {
        using var engine = OoxmlEngine.Create(OoxmlFormat.Wordprocessing);
        var doc = (WordprocessingDocument)engine.Package;
        var main = doc.AddMainDocumentPart();
        main.Document = new Document(
            new Body());
        main.Document.Save();

        var first = engine.Save();
        var second = engine.Save();

        first.Length.ShouldBeGreaterThan(0);
        second.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Save_OnReadOnlyPackage_ThrowsInvalidOperation()
    {
        // Produce a valid presentation, then reopen it read-only and confirm Save() is guarded.
        byte[] bytes;
        using (var writable = OoxmlEngine.Create(OoxmlFormat.Presentation))
        {
            var doc = (PresentationDocument)writable.Package;
            var presPart = doc.AddPresentationPart();
            presPart.Presentation = new Presentation(
                new SlideIdList(),
                new SlideSize { Cx = 9144000, Cy = 6858000 },
                new NotesSize { Cx = 6858000, Cy = 9144000 });
            presPart.Presentation.Save();
            bytes = writable.Save();
        }

        using var readOnly = OoxmlEngine.Open(bytes, false);
        Should.Throw<InvalidOperationException>(() => readOnly.Save());
    }
}
