using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models.Themes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses a slide layout OPC part into a <see cref="SlideLayout" />.
/// </summary>
internal sealed class LayoutParser(OpcPackage package, MediaStore mediaStore)
{
    /// <summary>
    ///     Parses the layout at <paramref name="partUri" /> and returns a <see cref="SlideLayout" />.
    /// </summary>
    public SlideLayout Parse(string partUri, string relationshipId)
    {
        var part = package.TryGetPart(partUri);
        if (part == null)
            return new SlideLayout { PartUri = partUri, RelationshipId = relationshipId };

        var doc = OoXmlHelper.ParseXml(part.Data);
        var root = doc.Root;

        var layout = new SlideLayout
        {
            PartUri = partUri,
            RelationshipId = relationshipId
        };

        if (root == null) return layout;

        layout.Name = root.GetAttr("name", string.Empty);
        layout.LayoutType = ParseLayoutType(root.GetAttr("type", string.Empty));
        layout.RawElement = root;

        // Parse shapes from the common slide data
        var cSld = root.Element(PmlNames.CommonSlideData);
        var spTree = cSld?.Element(PmlNames.ShapeTree);

        if (spTree == null) return layout;

        var shapeParser = new ShapeParser(package, mediaStore);
        shapeParser.ParseTree(spTree, layout.Shapes);

        return layout;
    }

    private static LayoutType ParseLayoutType(string value) => value switch
    {
        "blank" => LayoutType.Blank,
        "title" => LayoutType.Title,
        "tx" or "obj" or "twoObj" => LayoutType.TitleAndContent,
        "twoTxTwoObj" => LayoutType.TitleAndTwoContent,
        "titleOnly" => LayoutType.TitleOnly,
        "secHead" => LayoutType.SectionHeader,
        "twoTx" => LayoutType.TwoTextColumns,
        "vertTx" => LayoutType.TitleAndVerticalText,
        "picTx" => LayoutType.PictureWithCaption,
        "ctrTitle" => LayoutType.TitleSlide,
        _ => LayoutType.Custom
    };
}
