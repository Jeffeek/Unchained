using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Charts;
using Unchained.Xlsx.Parsing;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

/// <summary>Tests for <see cref="ChartXml" /> — the worksheet chart-part read/write facade.</summary>
public sealed class ChartXmlTests
{
    [Fact]
    public void Parse_Null_ReturnsEmptyModel()
    {
        var model = ChartXml.Parse(null);

        model.ShouldNotBeNull();
        model.Data.Series.ShouldBeEmpty();
    }

    [Fact]
    public void WriteThenParse_RoundTripsChartType()
    {
        var original = new ChartModel { Type = ChartType.Pie };
        original.Data.Categories.Add("A");
        var series = new ChartSeries { Name = "S1" };
        series.Values.Add(42.0);
        original.Data.Series.Add(series);

        var bytes = ChartXml.Write(original);
        var root = XDocument.Load(new MemoryStream(bytes)).Root;
        var parsed = ChartXml.Parse(root);

        parsed.Type.ShouldBe(ChartType.Pie);
        parsed.Data.Series.Count.ShouldBe(1);
        parsed.Data.Series[0].Values.ShouldContain(42.0);
    }
}
