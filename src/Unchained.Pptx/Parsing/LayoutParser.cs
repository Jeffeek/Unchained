using Unchained.Pptx.Core.Xml;
using System.Xml.Linq;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Media;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses a slide layout OPC part into a <see cref="SlideLayout"/>.
/// </summary>
internal sealed class LayoutParser
{
    private readonly OpcPackage _package;
    private readonly MediaStore _mediaStore;

    public LayoutParser(OpcPackage package, MediaStore mediaStore)
    {
        _package = package;
        _mediaStore = mediaStore;
    }

    /// <summary>
    /// Parses the layout at <paramref name="partUri"/> and returns a <see cref="SlideLayout"/>.
    /// </summary>
    public SlideLayout Parse(string partUri, string relationshipId)
    {
        var part = _package.TryGetPart(partUri);
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
        if (spTree != null)
        {
            var shapeParser = new ShapeParser(_package, _mediaStore);
            shapeParser.ParseTree(spTree, layout.Shapes);
        }

        return layout;
    }

    private static Models.Themes.LayoutType ParseLayoutType(string value) => value switch
    {
        "blank" => Models.Themes.LayoutType.Blank,
        "title" => Models.Themes.LayoutType.Title,
        "tx" or "obj" or "twoObj" => Models.Themes.LayoutType.TitleAndContent,
        "twoTxTwoObj" => Models.Themes.LayoutType.TitleAndTwoContent,
        "titleOnly" => Models.Themes.LayoutType.TitleOnly,
        "secHead" => Models.Themes.LayoutType.SectionHeader,
        "twoTx" => Models.Themes.LayoutType.TwoTextColumns,
        "vertTx" => Models.Themes.LayoutType.TitleAndVerticalText,
        "picTx" => Models.Themes.LayoutType.PictureWithCaption,
        "ctrTitle" => Models.Themes.LayoutType.TitleSlide,
        _ => Models.Themes.LayoutType.Custom
    };
}
