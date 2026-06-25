using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

/// <summary>
///     Coverage for <see cref="NotesWriter" />: the preserved-raw-XML round-trip path, the
///     rebuild-from-text-frame path (slide-image placeholder + notes placeholder + colour map),
///     and the null return when there is nothing to write.
/// </summary>
public sealed class NotesWriterTests
{
    private static readonly XNamespace P = PmlNames.Pml;

    [Fact]
    public void Write_NoTextNoRaw_ReturnsNull() =>
        NotesWriter.Write(new NotesSlide()).ShouldBeNull();

    [Fact]
    public void Write_PreservedRawElement_RoundTripsVerbatim()
    {
        var raw = new XElement(P + "notes", new XElement(P + "cSld"));
        var notes = new NotesSlide { RawElement = raw };
        var doc = NotesWriter.Write(notes);
        doc.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("notes");
    }

    [Fact]
    public void Write_FromTextFrame_BuildsNotesStructure()
    {
        var notes = new NotesSlide { NotesText = "Hello\nWorld" };
        var doc = NotesWriter.Write(notes);

        doc.ShouldNotBeNull();
        var root = doc.Root!;
        root.Name.LocalName.ShouldBe("notes");

        // Slide-image placeholder + notes placeholder both present.
        var shapes = root.Descendants(P + "sp").ToList();
        shapes.Count.ShouldBe(2);
        shapes.Descendants(P + "ph").Any(static ph => (string?)ph.Attribute("type") == "sldImg").ShouldBeTrue();
        shapes.Descendants(P + "ph").Any(static ph => (string?)ph.Attribute("type") == "body").ShouldBeTrue();

        // Colour-map override present.
        root.Elements(P + "clrMapOvr").ShouldNotBeEmpty();
    }

    [Fact]
    public void Write_FromExplicitFrame_UsesTextWriterShape()
    {
        var notes = new NotesSlide { NotesTextFrame = new TextFrame { PlainText = "Frame text" } };
        var doc = NotesWriter.Write(notes);
        doc.ShouldNotBeNull();
        doc.Descendants().Any(static e => e.Name.LocalName == "txBody").ShouldBeTrue();
    }
}
