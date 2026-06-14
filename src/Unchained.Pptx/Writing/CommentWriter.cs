using System.Xml.Linq;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core.Xml;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes slide comments to the <c>&lt;p:cmLst&gt;</c> format used in a
///     <c>ppt/comments/commentN.xml</c> OPC part.
/// </summary>
internal static class CommentWriter
{
    /// <summary>
    ///     Generates the comment part XML for a slide's comment list.
    /// </summary>
    public static XDocument Write(IReadOnlyList<Comment> comments)
    {
        var pml = PmlNames.Pml;
        var root = new XElement(
            pml + "cmLst",
            new XAttribute(XNamespace.Xmlns + "p", pml.NamespaceName)
        );

        foreach (var comment in comments)
        {
            var cm = new XElement(
                pml + "cm",
                new XAttribute("authorId", comment.Author.Id),
                new XAttribute("dt", comment.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
                new XAttribute("idx", comment.Index)
            );

            cm.Add(
                new XElement(
                    pml + "pos",
                    new XAttribute("x", comment.Position.X.Value),
                    new XAttribute("y", comment.Position.Y.Value)
                )
            );

            cm.Add(new XElement(pml + "text", comment.Text));

            root.Add(cm);
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
    }
}
