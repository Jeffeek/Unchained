using Shouldly;
using Unchained.Ooxml.Charts;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

/// <summary>
///     Unit tests for <see cref="ChartWriter" /> — serializes a <see cref="ChartModel" />
///     to chart-part XML. Exercises the writer in isolation; parser round-trips live in the
///     integration suite.
/// </summary>
public sealed class ChartWriterTests
{
    [Fact]
    public void Write_LineChart_ProducesChartSpaceRoot()
    {
        var model = new ChartModel { Type = ChartType.Line, Title = "Test Chart" };
        model.Data.Categories.AddRange(["A", "B", "C"]);
        var s = new ChartSeries { Name = "Series 1" };
        s.Values.AddRange([1.0, 2.0, 3.0]);
        model.Data.Series.Add(s);

        var bytes = ChartWriter.Write(model);
        bytes.ShouldNotBeEmpty();

        var doc = OoXmlHelper.ParseXml(bytes);
        doc.Root.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("chartSpace");
    }
}
