using System.Xml.Linq;
using Unchained.Pptx.Core.Opc;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Media;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses a slide master OPC part into a <see cref="MasterSlide"/>, including its
/// associated theme and all slide layouts.
/// </summary>
internal sealed class MasterParser
{
    private readonly OpcPackage _package;
    private readonly MediaStore _mediaStore;

    public MasterParser(OpcPackage package, MediaStore mediaStore)
    {
        _package = package;
        _mediaStore = mediaStore;
    }

    /// <summary>
    /// Parses the master at <paramref name="partUri"/> and returns a fully populated
    /// <see cref="MasterSlide"/> with theme and layouts.
    /// </summary>
    public MasterSlide Parse(string partUri, string relationshipId)
    {
        var part = _package.TryGetPart(partUri);
        if (part == null)
            return new MasterSlide { PartUri = partUri, RelationshipId = relationshipId };

        var doc = OoXmlHelper.ParseXml(part.Data);
        var root = doc.Root;

        var master = new MasterSlide
        {
            PartUri = partUri,
            RelationshipId = relationshipId,
            RawElement = root
        };

        if (root == null) return master;

        // Parse theme
        var themeRel = part.FindRelationship(PmlNames.RelTypeTheme);
        if (themeRel != null)
        {
            var themeUri = part.ResolveUri(themeRel.TargetUri);
            var themePart = _package.TryGetPart(themeUri);
            if (themePart != null)
                master.Theme = ThemeParser.Parse(themePart.Data);
        }

        // Parse shapes
        var cSld = root.Element(PmlNames.CommonSlideData);
        var spTree = cSld?.Element(PmlNames.ShapeTree);
        if (spTree != null)
        {
            var shapeParser = new ShapeParser(_package, _mediaStore);
            shapeParser.ParseTree(spTree, master.Shapes);
        }

        // Parse layouts
        var layoutParser = new LayoutParser(_package, _mediaStore);
        foreach (var layoutRel in part.FindRelationships(PmlNames.RelTypeSlideLayout))
        {
            var layoutUri = part.ResolveUri(layoutRel.TargetUri);
            var layout = layoutParser.Parse(layoutUri, layoutRel.Id);
            layout.Master = master;
            master.Layouts.Add(layout);
        }

        return master;
    }
}
