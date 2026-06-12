using System.Xml.Linq;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes the <see cref="CommentAuthorCollection" /> to the
///     <c>ppt/commentAuthors.xml</c> OPC part format.
/// </summary>
internal static class CommentAuthorWriter
{
    /// <summary>Generates the <c>&lt;p:cmAuthorLst&gt;</c> XML document.</summary>
    public static XDocument Write(CommentAuthorCollection authors)
    {
        var pml = PmlNames.Pml;
        var root = new XElement(pml + "cmAuthorLst",
            new XAttribute(XNamespace.Xmlns + "p", pml.NamespaceName));

        foreach (var author in authors)
        {
            root.Add(new XElement(pml + "cmAuthor",
                new XAttribute("id", author.Id),
                new XAttribute("name", author.Name),
                new XAttribute("initials", author.Initials),
                new XAttribute("lastIdx", author.LastIndex),
                new XAttribute("clrIdx", author.Id % 8))); // 8 color slots in PowerPoint
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
    }
}
