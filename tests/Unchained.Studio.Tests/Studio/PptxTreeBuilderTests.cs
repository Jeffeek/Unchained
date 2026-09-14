using Unchained.Ooxml;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models.Shapes;
using Unchained.Studio.Models;
using Unchained.Studio.Studio.Pptx;

namespace Unchained.Studio.Tests.Studio;

/// <summary>
///     Tests for <see cref="PptxTreeBuilder" /> — builds the navigable tree for a presentation:
///     document → Properties, Slides (with shapes), Masters (theme + layouts), Media.
/// </summary>
public sealed class PptxTreeBuilderTests
{
    private static PresentationDocument NewDocument()
    {
        using var processor = new PresentationProcessor();
        return processor.CreateBlank();
    }

    [Fact]
    public void Build_Root_IsExpandedDocumentWithFourChildren()
    {
        var document = NewDocument();

        var root = PptxTreeBuilder.Build(document, "deck.pptx");

        root.Label.ShouldBe("deck.pptx");
        root.NodeType.ShouldBe(TreeNodeType.Document);
        root.IsExpanded.ShouldBeTrue();
        root.Children.Count.ShouldBe(4);
        root.Children[0].NodeType.ShouldBe(TreeNodeType.Metadata);
        root.Children[1].NodeType.ShouldBe(TreeNodeType.Pages);
    }

    [Fact]
    public void Build_BlankPresentation_ReportsEmptySlidesAndMedia()
    {
        var document = NewDocument();

        var root = PptxTreeBuilder.Build(document, "deck.pptx");

        root.Children[1].Label.ShouldBe("Slides (0)");
        root.Children[1].Children.ShouldBeEmpty();
        root.Children[3].Label.ShouldBe("Media (0 images)");
    }

    [Fact]
    public void Build_MastersNode_ContainsMasterWithThemeAndLayouts()
    {
        var document = NewDocument();

        var mastersNode = PptxTreeBuilder.Build(document, "deck.pptx").Children[2];

        mastersNode.Label.ShouldBe("Masters (1)");
        var masterNode = mastersNode.Children.ShouldHaveSingleItem();
        masterNode.NodeType.ShouldBe(TreeNodeType.Master);

        masterNode.Children[0].NodeType.ShouldBe(TreeNodeType.Theme);
        masterNode.Children.Skip(1).ShouldAllBe(static n => n.NodeType == TreeNodeType.Layout);
        masterNode.Children.Count(static n => n.NodeType == TreeNodeType.Layout).ShouldBe(1);
    }

    [Fact]
    public void Build_SlideWithShape_AddsSlideAndShapeNodes()
    {
        var document = NewDocument();
        var slide = document.Slides.AddBlank(document.Masters[0].Layouts[0]);
        slide.Shapes.AddShape(
            AutoShapeType.Rectangle,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2)
        );

        var slidesNode = PptxTreeBuilder.Build(document, "deck.pptx").Children[1];

        slidesNode.Label.ShouldBe("Slides (1)");
        var slideNode = slidesNode.Children.ShouldHaveSingleItem();
        slideNode.Label.ShouldBe("Slide 1");
        slideNode.NodeType.ShouldBe(TreeNodeType.Slide);

        var shapeNode = slideNode.Children.ShouldHaveSingleItem();
        shapeNode.NodeType.ShouldBe(TreeNodeType.Shape);
        shapeNode.Label.ShouldContain("Rectangle");
    }

    [Fact]
    public void Build_HiddenSlide_AppendsHiddenSuffix()
    {
        var document = NewDocument();
        var slide = document.Slides.AddBlank(document.Masters[0].Layouts[0]);
        slide.IsHidden = true;

        var slideNode = PptxTreeBuilder.Build(document, "deck.pptx").Children[1].Children[0];

        slideNode.Label.ShouldBe("Slide 1 (hidden)");
    }
}
