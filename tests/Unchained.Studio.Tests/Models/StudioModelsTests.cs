using Unchained.Studio.Models;

namespace Unchained.Studio.Tests.Models;

/// <summary>Tests for the plain Studio model types <see cref="PropertyBag" /> and <see cref="TreeNode" />.</summary>
public sealed class StudioModelsTests
{
    [Fact]
    public void PropertyBag_Empty_SetsTitleAndLeavesRestDefault()
    {
        var bag = PropertyBag.Empty("Nothing selected");

        bag.Title.ShouldBe("Nothing selected");
        bag.Subtitle.ShouldBeNull();
        bag.Groups.ShouldBeEmpty();
        bag.RawText.ShouldBeNull();
    }

    [Fact]
    public void PropertyEntry_DefaultsToTextKindAndEmptyStrings()
    {
        var entry = new PropertyEntry();

        entry.Key.ShouldBe(string.Empty);
        entry.DisplayValue.ShouldBe(string.Empty);
        entry.Kind.ShouldBe(PropertyValueKind.Text);
        entry.CopyValue.ShouldBeNull();
    }

    [Fact]
    public void TreeNode_Id_IsUniquePerInstance()
    {
        var a = new TreeNode();
        var b = new TreeNode();

        a.Id.ShouldNotBeNullOrWhiteSpace();
        a.Id.ShouldNotBe(b.Id);
    }

    [Fact]
    public void TreeNode_HasChildren_FalseWhenEmptyAndNotLazy()
    {
        var node = new TreeNode();

        node.HasChildren.ShouldBeFalse();
    }

    [Fact]
    public void TreeNode_HasChildren_TrueWhenChildrenPresent()
    {
        var node = new TreeNode { Children = [new TreeNode()] };

        node.HasChildren.ShouldBeTrue();
    }

    [Fact]
    public void TreeNode_HasChildren_TrueWhenLazyEvenWithNoLoadedChildren()
    {
        var node = new TreeNode { HasLazyChildren = true };

        node.Children.ShouldBeEmpty();
        node.HasChildren.ShouldBeTrue();
    }
}
