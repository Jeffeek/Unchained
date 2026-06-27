using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses <c>&lt;p:cmLst&gt;</c> XML into a list of <see cref="Comment" /> objects.
/// </summary>
internal static class CommentParser
{
    /// <summary>
    ///     Parses all <c>&lt;p:cm&gt;</c> elements from <paramref name="cmLstRoot" />
    ///     and adds them to <paramref name="slide" />.
    /// </summary>
    public static void Parse(XElement cmLstRoot, Slide slide, CommentAuthorCollection authors)
    {
        var pml = PmlNames.Pml;

        foreach (var cm in cmLstRoot.Elements(pml + "cm"))
        {
            var authorIdRaw = cm.GetAttr("authorId");
            if (!uint.TryParse(authorIdRaw, out var authorId)) continue;

            var author = authors.FindById(authorId);
            if (author == null) continue;

            var idxRaw = cm.GetAttr(CmlNames.AttributeIndex);
            if (!uint.TryParse(idxRaw, out var idx)) continue;

            // Update the author's last index so new comments don't conflict
            if (idx > author.LastIndex)
                author.LastIndex = idx;

            // Position
            var posEl = cm.Element(pml + "pos");
            var posX = posEl != null ? posEl.GetAttrEmu("x") : Emu.Zero;
            var posY = posEl != null ? posEl.GetAttrEmu("y") : Emu.Zero;

            // Timestamp
            var dtRaw = cm.GetAttr("dt");
            var createdAt = DateTimeOffset.TryParse(dtRaw, out var dt) ? dt : DateTimeOffset.UtcNow;

            // Text
            var text = cm.Element(pml + "text")?.Value ?? string.Empty;

            var comment = new Comment(author, text, new SlidePosition(posX, posY), createdAt, idx);
            slide.AddParsedComment(comment);
        }
    }
}
