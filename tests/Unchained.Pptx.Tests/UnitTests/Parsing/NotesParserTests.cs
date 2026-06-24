using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Slides;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

public sealed class NotesParserTests
{
    private static readonly XNamespace P = PmlNames.Pml;
    private static readonly XNamespace A = DmlNames.Dml;

    private static XElement BodyShape(string text, string? phType = "body", string? idx = null)
    {
        var ph = new XElement(PmlNames.Placeholder);
        if (phType != null) ph.Add(new XAttribute("type", phType));
        if (idx != null) ph.Add(new XAttribute("idx", idx));

        return new XElement(
            PmlNames.Shape,
            new XElement(
                PmlNames.NonVisualShapeProperties,
                new XElement(PmlNames.ApplicationNonVisualProperties, ph)
            ),
            new XElement(
                PmlNames.TextBody,
                new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", text)))
            )
        );
    }

    private static XElement NotesRoot(params XElement[] shapes) =>
        new(
            P + "notes",
            new XElement(PmlNames.CommonSlideData, new XElement(PmlNames.ShapeTree, shapes.Cast<object>().ToArray()))
        );

    [Fact]
    public void Parse_BodyPlaceholder_ReadsText()
    {
        var notes = new NotesSlide();
        NotesParser.Parse(NotesRoot(BodyShape("speaker notes")), notes);
        notes.NotesText.ShouldBe("speaker notes");
    }

    [Fact]
    public void Parse_NoShapeTree_LeavesNotesEmpty()
    {
        var notes = new NotesSlide();
        NotesParser.Parse(new XElement(P + "notes"), notes);
        notes.NotesText.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_ShapeWithoutPlaceholder_IsSkipped()
    {
        var notes = new NotesSlide();
        var sp = new XElement(
            PmlNames.Shape,
            new XElement(PmlNames.NonVisualShapeProperties, new XElement(PmlNames.ApplicationNonVisualProperties)),
            new XElement(PmlNames.TextBody, new XElement(A + "p", new XElement(A + "r", new XElement(A + "t", "x"))))
        );
        NotesParser.Parse(NotesRoot(sp), notes);
        notes.NotesText.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_PlaceholderByIndex1_ReadsText()
    {
        var notes = new NotesSlide();
        NotesParser.Parse(NotesRoot(BodyShape("idx notes", phType: null, idx: "1")), notes);
        notes.NotesText.ShouldBe("idx notes");
    }

    [Fact]
    public void Parse_NonBodyNonIndexPlaceholder_IsSkipped()
    {
        var notes = new NotesSlide();
        NotesParser.Parse(NotesRoot(BodyShape("title text", phType: "title")), notes);
        notes.NotesText.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_PreservesRawElement()
    {
        var notes = new NotesSlide();
        var root = NotesRoot(BodyShape("anything"));
        NotesParser.Parse(root, notes);
        notes.RawElement.ShouldBeSameAs(root);
    }

    [Fact]
    public void Parse_BodyPlaceholderWithoutTextBody_BreaksWithoutFrame()
    {
        // Body placeholder shape but no <p:txBody> → the break path leaves the frame unset.
        var ph = new XElement(PmlNames.Placeholder, new XAttribute("type", "body"));
        var sp = new XElement(
            PmlNames.Shape,
            new XElement(
                PmlNames.NonVisualShapeProperties,
                new XElement(PmlNames.ApplicationNonVisualProperties, ph)
            )
        );
        var notes = new NotesSlide();
        NotesParser.Parse(NotesRoot(sp), notes);
        notes.NotesTextFrame.ShouldBeNull();
    }
}
