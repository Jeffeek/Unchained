using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses <c>&lt;p:cmAuthorLst&gt;</c> XML into a <see cref="CommentAuthorCollection" />.
/// </summary>
internal static class CommentAuthorParser
{
    /// <summary>
    ///     Reads all <c>&lt;p:cmAuthor&gt;</c> elements from <paramref name="rootEl" />
    ///     and adds them to <paramref name="authors" />.
    /// </summary>
    public static void Parse(XElement rootEl, CommentAuthorCollection authors)
    {
        var pml = PmlNames.Pml;

        foreach (var el in rootEl.Elements(pml + "cmAuthor"))
        {
            var idRaw = el.GetAttr(PmlNames.AttributeId);
            if (!uint.TryParse(idRaw, out var id)) continue;

            var name = el.GetAttr(PmlNames.AttributeName, string.Empty);
            var initials = el.GetAttr("initials", name.Length > 0 ? name[..1] : "?");
            var lastIdxRaw = el.GetAttr("lastIdx");
            uint.TryParse(lastIdxRaw, out var lastIdx);

            var author = new CommentAuthor(id, name, initials) { LastIndex = lastIdx };
            authors.AddParsed(author);
        }
    }
}
