using System.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models.Shapes;
using Unchained.Studio.Models;
using Unchained.Studio.Studio.Pptx;

namespace Unchained.Studio.Tests.Studio;

/// <summary>
///     Tests for <see cref="PptxPropertyAdapter" /> — turns a selected PPTX tree node into a
///     <see cref="PropertyBag" /> for the properties panel.
/// </summary>
public sealed class PptxPropertyAdapterTests
{
    private static PresentationDocument NewDocument()
    {
        using var processor = new PresentationProcessor();
        return processor.CreateBlank();
    }

    private static PropertyEntry Entry(PropertyBag bag, string key) =>
        bag.Groups.SelectMany(static g => g.Entries).First(e => e.Key == key);

    [Fact]
    public void Build_MetadataNode_ReturnsFourPropertyGroups()
    {
        var document = NewDocument();
        document.Properties.Title = "Deck";
        var node = new TreeNode { NodeType = TreeNodeType.Metadata, Payload = document.Properties };

        var bag = PptxPropertyAdapter.Build(node);

        bag.Title.ShouldBe("Document Properties");
        bag.Groups.Select(static g => g.Header).ShouldBe(["Core", "Dates", "Application", "Statistics"]);
        Entry(bag, "Title").DisplayValue.ShouldBe("Deck");
        Entry(bag, "Slides").Kind.ShouldBe(PropertyValueKind.Number);
    }

    [Fact]
    public void Build_SlideNode_ReturnsSlideIdentity()
    {
        var document = NewDocument();
        var slide = document.Slides.AddBlank(document.Masters[0].Layouts[0]);
        var node = new TreeNode { NodeType = TreeNodeType.Slide, Payload = slide };

        var bag = PptxPropertyAdapter.Build(node);

        bag.Title.ShouldBe($"Slide {slide.SlideNumber}");
        Entry(bag, "Hidden").Kind.ShouldBe(PropertyValueKind.Boolean);
        Entry(bag, "Layout").DisplayValue.ShouldBe("Blank");
    }

    [Fact]
    public void Build_AutoShapeNode_IncludesIdentityAutoShapeAndGeometryGroups()
    {
        var document = NewDocument();
        var slide = document.Slides.AddBlank(document.Masters[0].Layouts[0]);
        var shape = slide.Shapes.AddShape(
            AutoShapeType.Rectangle,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2)
        );
        var node = new TreeNode { NodeType = TreeNodeType.Shape, Payload = shape };

        var bag = PptxPropertyAdapter.Build(node);

        bag.Groups.Select(static g => g.Header).ShouldBe(["Identity", "Auto Shape", "Geometry"]);
        Entry(bag, "Preset").DisplayValue.ShouldBe("Rectangle");
        Entry(bag, "Width").DisplayValue.ShouldContain("EMU");
    }

    [Fact]
    public void Build_MasterNode_ReturnsMasterSummary()
    {
        var document = NewDocument();
        var node = new TreeNode { NodeType = TreeNodeType.Master, Payload = document.Masters[0] };

        var bag = PptxPropertyAdapter.Build(node);

        bag.Subtitle.ShouldBe("Slide Master");
        Entry(bag, "Layouts").Kind.ShouldBe(PropertyValueKind.Number);
    }

    [Fact]
    public void Build_LayoutNode_ReturnsLayoutSummary()
    {
        var document = NewDocument();
        var node = new TreeNode { NodeType = TreeNodeType.Layout, Payload = document.Masters[0].Layouts[0] };

        var bag = PptxPropertyAdapter.Build(node);

        bag.Subtitle.ShouldBe("Slide Layout");
        Entry(bag, "Layout type").DisplayValue.ShouldBe("Blank");
    }

    [Fact]
    public void Build_ThemeNode_ReturnsFontsAndColors()
    {
        var document = NewDocument();
        var node = new TreeNode { NodeType = TreeNodeType.Theme, Payload = document.Masters[0].Theme };

        var bag = PptxPropertyAdapter.Build(node);

        bag.Subtitle.ShouldBe("Theme");
        bag.Groups.Select(static g => g.Header).ShouldBe(["Fonts", "Colors"]);
        Entry(bag, "Accent 1").Kind.ShouldBe(PropertyValueKind.Hex);
    }

    [Fact]
    public void Build_ImageNode_ReturnsImageDetails()
    {
        var image = new EmbeddedImage("image/png", new byte[2048]);
        var node = new TreeNode { NodeType = TreeNodeType.Image, Payload = image };

        var bag = PptxPropertyAdapter.Build(node);

        bag.Title.ShouldBe("Embedded Image");
        Entry(bag, "Content type").DisplayValue.ShouldBe("image/png");
        Entry(bag, "Size").DisplayValue.ShouldContain("KB");
    }

    [Fact]
    public void Build_UnknownNodeType_ReturnsEmptyBagWithLabel()
    {
        var node = new TreeNode { NodeType = TreeNodeType.Document, Payload = NewDocument(), Label = "deck.pptx" };

        var bag = PptxPropertyAdapter.Build(node);

        bag.Title.ShouldBe("deck.pptx");
        bag.Groups.ShouldBeEmpty();
    }

    [Fact]
    public void Build_MatchingTypeButWrongPayload_ReturnsEmptyBag()
    {
        var node = new TreeNode { NodeType = TreeNodeType.Slide, Payload = "not a slide", Label = "bogus" };

        var bag = PptxPropertyAdapter.Build(node);

        bag.Title.ShouldBe("bogus");
        bag.Groups.ShouldBeEmpty();
    }
}
