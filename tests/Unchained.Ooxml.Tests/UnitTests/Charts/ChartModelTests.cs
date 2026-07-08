using Shouldly;
using Unchained.Ooxml.Charts;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Charts;

public sealed class ChartModelTests
{
    [Fact]
    public void Defaults_NonNullChildren()
    {
        var model = new ChartModel();
        model.Data.ShouldNotBeNull();
        model.Legend.ShouldNotBeNull();
        model.CategoryAxis.ShouldNotBeNull();
        model.ValueAxis.ShouldNotBeNull();
        model.DataLabels.ShouldNotBeNull();
    }

    [Fact]
    public void Axes_HaveExpectedKinds()
    {
        var model = new ChartModel();
        model.CategoryAxis.Kind.ShouldBe(ChartAxisKind.Category);
        model.ValueAxis.Kind.ShouldBe(ChartAxisKind.Value);
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var model = new ChartModel
        {
            Type = ChartType.Pie,
            Title = "Revenue",
            HasTitle = false,
            HasDataTable = true
        };
        model.Type.ShouldBe(ChartType.Pie);
        model.Title.ShouldBe("Revenue");
        model.HasTitle.ShouldBeFalse();
        model.HasDataTable.ShouldBeTrue();
    }
}
