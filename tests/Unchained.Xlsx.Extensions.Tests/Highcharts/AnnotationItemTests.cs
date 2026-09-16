using Shouldly;
using Unchained.Xlsx.Extensions.Highcharts.Models;
using Xunit;

namespace Unchained.Xlsx.Extensions.Tests.Highcharts;

/// <summary>Tests for <see cref="AnnotationItem" /> properties and collections.</summary>
public sealed class AnnotationItemTests
{
    [Fact]
    public void Constructor_InitializesCollections()
    {
        var item = new AnnotationItem();

        item.Shapes.ShouldNotBeNull();
        item.Shapes.ShouldBeEmpty();

        item.Labels.ShouldNotBeNull();
        item.Labels.ShouldBeEmpty();

        item.Points.ShouldNotBeNull();
        item.Points.ShouldBeEmpty();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var item = new AnnotationItem
        {
            Id = "anno1",
            Title = "Test Annotation"
        };

        item.Id.ShouldBe("anno1");
        item.Title.ShouldBe("Test Annotation");
    }

    [Fact]
    public void GetAdditionalProperties_ReturnsAdditionalPropertiesDictionary()
    {
        var props = new Dictionary<string, object> { ["custom"] = "value" };
        var item = new AnnotationItem { AdditionalProperties = props };

        item.GetAdditionalProperties().ShouldBeSameAs(props);
    }

    [Fact]
    public void GetAdditionalProperties_WhenNull_ReturnsNull()
    {
        var item = new AnnotationItem();
        item.GetAdditionalProperties().ShouldBeNull();
    }

    [Fact]
    public void Shapes_CanAddItems()
    {
        var item = new AnnotationItem();
        item.Shapes.Add(new ShapeItem());
        item.Shapes.Count.ShouldBe(1);
    }

    [Fact]
    public void Labels_CanAddItems()
    {
        var item = new AnnotationItem();
        item.Labels.Add(new LabelItem());
        item.Labels.Count.ShouldBe(1);
    }

    [Fact]
    public void Points_CanAddItems()
    {
        var item = new AnnotationItem();
        item.Points.Add(new AnnotationPoint());
        item.Points.Count.ShouldBe(1);
    }
}
