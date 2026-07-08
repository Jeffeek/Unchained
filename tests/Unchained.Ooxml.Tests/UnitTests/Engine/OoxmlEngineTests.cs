using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Shouldly;
using Unchained.Ooxml.Engine;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
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
            PreparePresentation(engine);
            bytes = engine.Save();
        }

        bytes.Length.ShouldBeGreaterThan(0);

        // Reopen the saved bytes through the engine — format must be detected as Presentation.
        using var reopened = OoxmlEngine.Open(bytes, false);
        reopened.Format.ShouldBe(OoxmlFormat.Presentation);
    }

    [Fact]
    public void Open_GarbageBytes_Throws() =>
        Should.Throw<Exception>(static () => OoxmlEngine.Open([1, 2, 3, 4], false));

    [Fact]
    public void Open_ValidOpcPackageWithNoRecognizedMainPart_Throws()
    {
        // A structurally valid OPC package whose only part is not a presentation/document/workbook
        // main part → DetectFormat falls through to the "could not determine format" throw.
        var package = OpcPackage.CreateEmpty();
        package.AddOrReplacePart("/custom/part.xml", OoxmlContentTypes.ApplicationXml, "<root/>"u8.ToArray());
        var bytes = package.Save();

        Should.Throw<OoXmlException>(() => OoxmlEngine.Open(bytes, false));
    }

    [Fact]
    public void Save_LeavesEngineUsable()
    {
        using var engine = OoxmlEngine.Create(OoxmlFormat.Wordprocessing);
        PrepareWordProcessing(engine);

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
            PreparePresentation(writable);
            bytes = writable.Save();
        }

        using var readOnly = OoxmlEngine.Open(bytes, false);
        Should.Throw<InvalidOperationException>(() => readOnly.Save());
    }

    [Fact]
    public void SaveTo_WritesBytesToDestination()
    {
        using var engine = OoxmlEngine.Create(OoxmlFormat.Wordprocessing);
        PrepareWordProcessing(engine);

        using var destination = new MemoryStream();
        engine.SaveTo(destination);
        destination.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void SaveTo_NullDestination_Throws()
    {
        using var engine = OoxmlEngine.Create(OoxmlFormat.Presentation);
        Should.Throw<ArgumentNullException>(() => engine.SaveTo(null!));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var engine = OoxmlEngine.Create(OoxmlFormat.Spreadsheet);
        engine.Dispose();
        Should.NotThrow(() => engine.Dispose());
    }

    [Fact]
    public void Save_AfterDispose_Throws()
    {
        var engine = OoxmlEngine.Create(OoxmlFormat.Presentation);
        engine.Dispose();
        Should.Throw<ObjectDisposedException>(() => engine.Save());
    }

    [Fact]
    public void Open_WordprocessingPackage_DetectsWordFormat()
    {
        byte[] bytes;
        using (var engine = OoxmlEngine.Create(OoxmlFormat.Wordprocessing))
        {
            PrepareWordProcessing(engine);
            bytes = engine.Save();
        }

        using var reopened = OoxmlEngine.Open(bytes, false);
        reopened.Format.ShouldBe(OoxmlFormat.Wordprocessing);
    }

    [Fact]
    public void Open_SpreadsheetPackage_DetectsSpreadsheetFormat()
    {
        byte[] bytes;
        using (var engine = OoxmlEngine.Create(OoxmlFormat.Spreadsheet))
        {
            PrepareSpreadsheet(engine);
            bytes = engine.Save();
        }

        using var reopened = OoxmlEngine.Open(bytes, false);
        reopened.Format.ShouldBe(OoxmlFormat.Spreadsheet);
    }

    // ── Helpers (extracted from duplicated inline SDK setup) ──────────────────

    private static void PreparePresentation(OoxmlEngine engine)
    {
        var doc = (PresentationDocument)engine.Package;
        var presPart = doc.AddPresentationPart();
        presPart.Presentation = new Presentation(
            new SlideIdList(),
            new SlideSize { Cx = 9144000, Cy = 6858000 },
            new NotesSize { Cx = 6858000, Cy = 9144000 }
        );
        presPart.Presentation.Save();
    }

    private static void PrepareWordProcessing(OoxmlEngine engine)
    {
        var doc = (WordprocessingDocument)engine.Package;
        var main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body());
        main.Document.Save();
    }

    private static void PrepareSpreadsheet(OoxmlEngine engine)
    {
        var doc = (SpreadsheetDocument)engine.Package;
        var wbPart = doc.AddWorkbookPart();
        wbPart.Workbook = new Workbook(new Sheets());
        wbPart.Workbook.Save();
    }
}
