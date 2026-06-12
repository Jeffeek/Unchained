using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses a <c>&lt;p:notes&gt;</c> XML root into a <see cref="NotesSlide" /> model.
/// </summary>
internal static class NotesParser
{
    /// <summary>
    ///     Extracts the speaker notes text from the notes XML root and populates
    ///     <paramref name="notes" />. The <c>NotesText</c> shortcut and the full
    ///     <c>NotesTextFrame</c> are both populated.
    /// </summary>
    public static void Parse(XElement notesRoot, NotesSlide notes)
    {
        var pml = PmlNames.Pml;

        // Preserve the full notes XML so a save re-emits it verbatim (the notes model captures
        // only the body text, not the notes-master reference, slide-image placeholder, or
        // formatting). NotesWriter honours this when present.
        notes.RawElement = notesRoot;

        // Find the body placeholder (type="body" idx="1") inside the shape tree
        var spTree = notesRoot.Element(pml + "cSld")?.Element(pml + "spTree");
        if (spTree == null) return;

        foreach (var sp in spTree.Elements(pml + "sp"))
        {
            var phEl = sp.Element(pml + "nvSpPr")
                ?.Element(pml + "nvPr")
                ?.Element(pml + "ph");
            if (phEl == null) continue;

            var phType = phEl.GetAttr("type", string.Empty);
            // Notes text placeholder: type="body" or (type omitted) idx="1"
            if (phType != "body" && phEl.GetAttr("idx") != "1") continue;

            var txBody = sp.Element(pml + "txBody");
            if (txBody == null) break;

            var textFrame = TextParser.ParseTextBody(txBody);
            notes.NotesTextFrame = textFrame;
            break;
        }
    }
}
