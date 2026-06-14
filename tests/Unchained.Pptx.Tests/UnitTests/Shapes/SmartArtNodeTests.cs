using Shouldly;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Shapes;

public sealed class SmartArtNodeTests
{
    [Fact]
    public void Defaults_EmptyTextNoChildren()
    {
        var node = new SmartArtNode();
        node.ModelId.ShouldBe(string.Empty);
        node.Text.ShouldBe(string.Empty);
        node.Children.ShouldBeEmpty();
    }

    [Fact]
    public void AddChild_AppendsAndReturnsChild()
    {
        var root = new SmartArtNode { Text = "Root" };
        var child = root.AddChild("Child");
        child.Text.ShouldBe("Child");
        root.Children.Count.ShouldBe(1);
        root.Children[0].ShouldBeSameAs(child);
    }

    [Fact]
    public void AddChild_Nested_BuildsHierarchy()
    {
        var root = new SmartArtNode();
        var child = root.AddChild("A");
        var grandchild = child.AddChild("B");
        root.Children[0].Children[0].ShouldBeSameAs(grandchild);
        grandchild.Text.ShouldBe("B");
    }
}
