using Shouldly;
using System.Text.Json.Nodes;
using Unchained.Ooxml.Charts;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Extensions.Highcharts;
using Unchained.Xlsx.Extensions.Highcharts.Models;
using Xunit;

namespace Unchained.Xlsx.Extensions.Tests.UnitTests;

/// <summary>
///     Tests for <see cref="Extensions" /> — extension methods for chart conversion.
/// </summary>
public class HighchartsExtensionsTests
{
    private static ChartDrawing BuildSimpleChart()
    {
        var drawing = new ChartDrawing
        {
            Chart =
            {
                Type = ChartType.ColumnClustered,
                Title = "Test Chart"
            }
        };

        drawing.Chart.Data.Categories.Add("A");
        drawing.Chart.Data.Categories.Add("B");

        var series = new ChartSeries { Name = "Sales" };
        series.Values.Add(10.0);
        series.Values.Add(20.0);
        drawing.Chart.Data.Series.Add(series);

        return drawing;
    }

    #region ToHighchartsJson

    [Fact]
    public void ToHighchartsJson_ReturnsValidJson()
    {
        var chart = BuildSimpleChart();

        var json = chart.ToHighchartsJson();

        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("\"chart\"");
        json.ShouldContain("\"series\"");
    }

    [Fact]
    public void ToHighchartsJson_ProducesLowercaseKeys()
    {
        var chart = BuildSimpleChart();

        var json = chart.ToHighchartsJson();

        json.ShouldContain("\"chart\"");
        json.ShouldContain("\"title\"");
        json.ShouldNotContain("\"Chart\"", Case.Sensitive);
        json.ShouldNotContain("\"Title\"", Case.Sensitive);
    }

    #endregion

    #region ToHighchartsObject

    [Fact]
    public void ToHighchartsObject_NoSettings_ReturnsValidOptions()
    {
        var chart = BuildSimpleChart();

        var options = chart.ToHighchartsObject();

        options.ShouldNotBeNull();
        options.Chart.ShouldNotBeNull();
        options.Series.ShouldNotBeEmpty();
    }

    [Fact]
    public void ToHighchartsObject_WithAdditionalProperties_MergesThem()
    {
        var chart = BuildSimpleChart();
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object>
            {
                ["customField"] = "customValue",
                ["numericField"] = 42
            }
        };

        var options = chart.ToHighchartsObject(settings);

        options.AdditionalProperties.ShouldNotBeNull();
        options.AdditionalProperties.ShouldContainKey("customField");
        options.AdditionalProperties["customField"].ShouldBe("customValue");
        options.AdditionalProperties["numericField"].ShouldBe(42);
    }

    [Fact]
    public void ToHighchartsObject_NullSettings_DoesNotThrow()
    {
        var chart = BuildSimpleChart();

        var options = chart.ToHighchartsObject(null);

        options.ShouldNotBeNull();
    }

    [Fact]
    public void ToHighchartsObject_SettingsWithNoAdditionalProperties_DoesNotCreateDict()
    {
        var chart = BuildSimpleChart();
        var settings = new HighchartsSettings();

        var options = chart.ToHighchartsObject(settings);

        options.AdditionalProperties.ShouldBeNull();
    }

    #endregion

    #region ToJson

    [Fact]
    public void ToJson_WithoutSettings_SerializesCorrectly()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "column" },
            Title = new TitleConfig { Text = "Test" }
        };

        var json = options.ToJson();

        json.ShouldContain("\"chart\"");
        json.ShouldContain("\"type\"");
        json.ShouldContain("\"column\"");
    }

    [Fact]
    public void ToJson_WithSettings_MergesAdditionalProperties()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "line" }
        };
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object>
            {
                ["accessibility"] = new { enabled = true }
            }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"accessibility\"");
        json.ShouldContain("\"enabled\"");
    }

    [Fact]
    public void ToJson_WithAllowOverrides_OverridesExistingProperties()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "bar" },
            AdditionalProperties = new Dictionary<string, object> { ["chart"] = "original" }
        };
        var settings = new HighchartsSettings
        {
            AllowOverrides = true,
            AdditionalProperties = new Dictionary<string, object> { ["chart"] = "overridden" }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"overridden\"");
    }

    [Fact]
    public void ToJson_WithoutAllowOverrides_PreservesExistingProperties()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "bar" }
        };
        var settings = new HighchartsSettings
        {
            AllowOverrides = false,
            AdditionalProperties = new Dictionary<string, object> { ["chart"] = new { type = "overridden" } }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"bar\"");
        json.ShouldNotContain("\"overridden\"");
    }

    [Fact]
    public void ToJson_NullProperties_AreOmitted()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "pie" },
            Subtitle = null,
            Drilldown = null
        };

        var json = options.ToJson();

        json.ShouldNotContain("\"subtitle\"");
        json.ShouldNotContain("\"drilldown\"");
    }

    [Fact]
    public void ToJson_ObjectAdditionalProperties_SerializesAsNestedObject()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "line" }
        };
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object>
            {
                ["responsive"] = new { rules = new[] { new { condition = new { maxWidth = 500 } } } }
            }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"responsive\"");
        json.ShouldContain("\"rules\"");
        json.ShouldContain("\"condition\"");
        json.ShouldContain("\"maxWidth\"");
    }

    [Fact]
    public void ToJson_StringAdditionalProperty_SerializesAsString()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "line" }
        };
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object> { ["lang"] = "en-US" }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"lang\"");
        json.ShouldContain("\"en-US\"");
    }

    [Fact]
    public void ToJson_BooleanAdditionalProperty_SerializesAsBoolean()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "bar" }
        };
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object> { ["styledMode"] = true }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"styledMode\"");
        json.ShouldContain("true");
    }

    [Fact]
    public void ToJson_NumericAdditionalProperty_SerializesAsNumber()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "scatter" }
        };
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object> { ["zoomType"] = 42 }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"zoomType\"");
        json.ShouldContain("42");
    }

    [Fact]
    public void ToJson_JsonNodeAdditionalProperty_PreservesNode()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "area" }
        };
        var jsonNode = JsonNode.Parse("{\"custom\":\"value\"}");
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object> { ["node"] = jsonNode! }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"node\"");
        json.ShouldContain("\"custom\"");
        json.ShouldContain("\"value\"");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToJson_EmptyAdditionalProperties_DoesNotThrow()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "pie" }
        };
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object>()
        };

        var json = options.ToJson(settings);

        json.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ToJson_OptionsWithAdditionalProperties_MergesIntoOutput()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "bubble" },
            AdditionalProperties = new Dictionary<string, object> { ["boost"] = new { enabled = true } }
        };

        var json = options.ToJson();

        json.ShouldContain("\"boost\"");
        json.ShouldContain("\"enabled\"");
    }

    [Fact]
    public void ToJson_JsonArrayAdditionalProperty_SerializesAsArray()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "line" }
        };
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object> { ["colors"] = new JsonArray("#FFFFFF", "#000000") }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"colors\"");
        json.ShouldContain("#FFFFFF");
        json.ShouldContain("#000000");
    }

    [Fact]
    public void ToJson_JsonObjectAdditionalProperty_SerializesAsObject()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "line" }
        };
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object> { ["boost"] = new JsonObject { ["enabled"] = true } }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"boost\"");
        json.ShouldContain("\"enabled\"");
    }

    [Fact]
    public void ToJson_JsonValueAdditionalProperty_SerializesAsScalar()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "line" }
        };
        var settings = new HighchartsSettings
        {
            AdditionalProperties = new Dictionary<string, object> { ["margin"] = JsonValue.Create(3.14) }
        };

        var json = options.ToJson(settings);

        json.ShouldContain("\"margin\"");
        json.ShouldContain("3.14");
    }

    [Fact]
    public void ToJson_ChildConfigAdditionalProperties_MergedIntoChildSubtree()
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig
            {
                Type = "line",
                AdditionalProperties = new Dictionary<string, object> { ["zoomType"] = "xy" }
            }
        };

        var json = options.ToJson();

        // The additional property must be merged as a direct key on the "chart" node,
        // not merely present somewhere in the tree (e.g. nested under additionalProperties).
        var root = JsonNode.Parse(json)!;
        root["chart"].ShouldNotBeNull();
        root["chart"]!["zoomType"]!.GetValue<string>().ShouldBe("xy");
    }

    [
        Theory,
        InlineData("navigator"),
        InlineData("pane")
    ]
    public void ToJson_NestedConfigAdditionalProperties_MergedIntoOwnSubtree(string section)
    {
        var options = new HighchartsOptions
        {
            Chart = new ChartConfig { Type = "line" },
            Navigator = new Navigator
            {
                AdditionalProperties = new Dictionary<string, object> { ["marker"] = "nav" }
            },
            Pane = new Pane
            {
                AdditionalProperties = new Dictionary<string, object> { ["marker"] = "pane" }
            }
        };

        var json = options.ToJson();

        var root = JsonNode.Parse(json)!;
        root[section].ShouldNotBeNull();
        root[section]!["marker"]!.GetValue<string>().ShouldBe(section == "navigator" ? "nav" : "pane");
    }

    #endregion

    #region Full options graph serialization

    /// <summary>
    ///     Serialising a fully-populated options graph must emit every optional section and every
    ///     nested config, exercising the whole model surface through the serializer.
    /// </summary>
    [Fact]
    public void ToJson_FullyPopulatedGraph_EmitsEverySection()
    {
        var options = BuildFullOptions();

        var json = options.ToJson();

        // Section presence (Shouldly string contains is case-insensitive, matching camelCase keys).
        json.ShouldContain("\"subtitle\"");
        json.ShouldContain("\"xaxis\"");
        json.ShouldContain("\"plotLines\"");
        json.ShouldContain("\"dateTimeLabelFormats\"");
        json.ShouldContain("\"plotOptions\"");
        json.ShouldContain("\"legend\"");
        json.ShouldContain("\"navigation\"");
        json.ShouldContain("\"drilldown\"");
        json.ShouldContain("\"annotations\"");
        json.ShouldContain("\"navigator\"");
        json.ShouldContain("\"pane\"");
        json.ShouldContain("\"exporting\"");
        json.ShouldContain("\"credits\"");
    }

    [Fact]
    public void ToJson_FullyPopulatedGraph_ProducesParseableJson()
    {
        var options = BuildFullOptions();

        var json = options.ToJson();

        Should.NotThrow(() => JsonNode.Parse(json)).ShouldNotBeNull();
    }

    private static HighchartsOptions BuildFullOptions() =>
        new()
        {
            Chart = new ChartConfig { Type = "line", Style = new StyleConfig() },
            Title = new TitleConfig { Text = "Title" },
            Subtitle = new SubtitleConfig { Text = "Subtitle" },
            XAxis = new AxisConfig
            {
                Categories = ["A", "B"],
                Labels = new LabelConfig { Style = new StyleConfig() },
                PlotLines = [new PlotLine { Value = 5, Color = "#000" }],
                Scrollbar = new ScrollbarConfig { Enabled = true },
                DateTimeLabelFormats = new DateTimeLabelFormats { Day = "%e" }
            },
            YAxis = [new YAxisConfig { Title = "Y" }],
            Series = [new SeriesConfig { Name = "S", Marker = new MarkerConfig { Symbol = "circle" } }],
            PlotOptions = new PlotOptions
            {
                Series = new PlotOptionsSeries
                {
                    Marker = new PlotOptionsMarker(),
                    DataLabels = new PlotOptionsDataLabels(),
                    States = new PlotOptionsStates
                    {
                        Hover = new PlotOptionsState { Halo = new PlotOptionsHalo() }
                    }
                },
                Pie = new PlotOptionsPie { DataLabels = new PlotOptionsDataLabels() },
                Bubble = new PlotOptionsBubble { MinSize = 5, MaxSize = 20 }
            },
            Legend = new LegendConfig { Enabled = true, Navigation = new LegendNavigation() },
            Tooltip = new Tooltip { Shared = true },
            Drilldown = new Drilldown
            {
                ActiveSeriesStyle = new DrilldownSeriesStyle(),
                ActiveAxisStyle = new DrilldownAxisStyle(),
                Series =
                {
                    ["parent"] = [new DrilldownSeries { ParentSeriesName = "parent", Data = [new SeriesConfig()] }]
                }
            },
            Annotations = new Annotations
            {
                Items =
                [
                    new AnnotationItem
                    {
                        Shapes = [new ShapeItem { Type = "rect" }],
                        Labels = [new LabelItem { Text = "L" }],
                        Points = [new AnnotationPoint { Series = 0, X = 0 }]
                    }
                ]
            },
            Navigator = new Navigator { Series = new SeriesConfig(), Scrollbar = new ScrollbarConfig() },
            Pane = new Pane { Background = [new BackgroundConfig { Shape = "arc" }] },
            Colors = ["#111111"],
            Exporting = new ExportingConfig
            {
                Enabled = true,
                Csv = new CsvExportConfig(),
                Buttons = new ExportingButtonsConfig()
            },
            Credits = new CreditsConfig { Enabled = false, Position = new CreditsPositionConfig() },
            TimeUseUtc = true
        };

    #endregion
}
