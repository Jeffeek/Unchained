using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Writing;

/// <summary>
/// Serializes a <see cref="NotesSlide"/> into a <c>&lt;p:notes&gt;</c> XML document.
/// </summary>
internal static class NotesWriter
{
    /// <summary>
    /// Generates the full <c>&lt;p:notes&gt;</c> XML for the given notes slide.
    /// Returns <see langword="null"/> when there is no text to write.
    /// </summary>
    public static XDocument? Write(NotesSlide notes)
    {
        var text = notes.NotesText;
        if (string.IsNullOrEmpty(text)) return null;

        var pml = PmlNames.Pml;
        var dml = DmlNames.Dml;
        var r = XNamespace.Get(
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        var notesEl = new XElement(PmlNames.Notes,
            new XAttribute(XNamespace.Xmlns + "p", pml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "a", dml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName));

        var cSld = new XElement(pml + "cSld");
        var spTree = new XElement(pml + "spTree");

        // Required group shape non-visual properties
        spTree.Add(new XElement(pml + "nvGrpSpPr",
            new XElement(pml + "cNvPr",
                new XAttribute("id", "1"),
                new XAttribute("name", string.Empty)),
            new XElement(pml + "cNvGrpSpPr"),
            new XElement(pml + "nvPr")));
        spTree.Add(new XElement(pml + "grpSpPr",
            new XElement(dml + "xfrm",
                new XElement(dml + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(dml + "ext", new XAttribute("cx", 0), new XAttribute("cy", 0)),
                new XElement(dml + "chOff", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(dml + "chExt", new XAttribute("cx", 0), new XAttribute("cy", 0)))));

        // Slide image placeholder (required by the spec)
        spTree.Add(new XElement(pml + "sp",
            new XElement(pml + "nvSpPr",
                new XElement(pml + "cNvPr", new XAttribute("id", "2"),
                    new XAttribute("name", "Slide Image Placeholder 1")),
                new XElement(pml + "cNvSpPr",
                    new XElement(dml + "spLocks",
                        new XAttribute("noGrp", "1"),
                        new XAttribute("noRot", "1"),
                        new XAttribute("noChangeAspect", "1"))),
                new XElement(pml + "nvPr",
                    new XElement(pml + "ph", new XAttribute("type", "sldImg")))),
            new XElement(pml + "spPr")));

        // Notes text placeholder
        var txBody = WriteNotesTextBody(notes, dml);
        spTree.Add(new XElement(pml + "sp",
            new XElement(pml + "nvSpPr",
                new XElement(pml + "cNvPr", new XAttribute("id", "3"),
                    new XAttribute("name", "Notes Placeholder 2")),
                new XElement(pml + "cNvSpPr",
                    new XElement(dml + "spLocks", new XAttribute("noGrp", "1"))),
                new XElement(pml + "nvPr",
                    new XElement(pml + "ph",
                        new XAttribute("type", "body"),
                        new XAttribute("idx", "1")))),
            new XElement(pml + "spPr"),
            txBody));

        cSld.Add(spTree);
        notesEl.Add(cSld);
        notesEl.Add(new XElement(pml + "clrMapOvr",
            new XElement(dml + "masterClrMapping")));

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), notesEl);
    }

    private static XElement WriteNotesTextBody(NotesSlide notes, XNamespace dml)
    {
        // If a full text frame is available, use TextWriter; otherwise emit plain text
        if (notes.NotesTextFrame != null)
            return TextWriter.WriteAsShape(notes.NotesTextFrame);

        // Plain text fallback
        var txBody = new XElement(PmlNames.TextBody);
        txBody.Add(new XElement(dml + "bodyPr"));
        txBody.Add(new XElement(dml + "lstStyle"));

        foreach (var line in notes.NotesText.Split('\n'))
        {
            var para = new XElement(dml + "p");
            if (!string.IsNullOrEmpty(line))
            {
                var run = new XElement(dml + "r");
                run.Add(new XElement(dml + "rPr",
                    new XAttribute("lang", "en-US"),
                    new XAttribute("dirty", "0")));
                run.Add(new XElement(dml + "t", line));
                para.Add(run);
            }
            txBody.Add(para);
        }

        return txBody;
    }
}
