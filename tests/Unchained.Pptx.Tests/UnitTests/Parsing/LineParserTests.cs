using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

public sealed class LineParserTests
{
    private static XElement WriteLn(LineFormat line)
    {
        var parent = new XElement(DmlNames.Dml + "spPr");
        LineWriter.Write(parent, line);
        return parent;
    }

    [Fact]
    public void Parse_NoLnElement_LeavesDefaults()
    {
        var line = new LineFormat();
        LineParser.Parse(new XElement(DmlNames.Dml + "spPr"), line);
        line.WidthPoints.ShouldBeNull();
        line.DashStyle.ShouldBe(LineDashStyle.Solid);
    }

    [Fact]
    public void Parse_Width_ConvertsEmuToPoints()
    {
        var ln = new XElement(DmlNames.Line, new XAttribute("w", "12700"));
        var parent = new XElement(DmlNames.Dml + "spPr", ln);
        var line = new LineFormat();
        LineParser.Parse(parent, line);
        line.WidthPoints.ShouldNotBeNull();
        line.WidthPoints.Value.ShouldBe(1.0, 0.001);
    }

    [
        Theory,
        InlineData("dot", LineDashStyle.Dot),
        InlineData("dash", LineDashStyle.Dash),
        InlineData("dashDot", LineDashStyle.DashDot),
        InlineData("lgDash", LineDashStyle.LongDash),
        InlineData("lgDashDot", LineDashStyle.LongDashDot),
        InlineData("lgDashDotDot", LineDashStyle.LongDashDotDot),
        InlineData("sysDash", LineDashStyle.SystemDash),
        InlineData("sysDot", LineDashStyle.SystemDot),
        InlineData("sysDashDot", LineDashStyle.SystemDashDot),
        InlineData("sysDashDotDot", LineDashStyle.SystemDashDotDot),
        InlineData("solid", LineDashStyle.Solid),
        InlineData("unknown", LineDashStyle.Solid)
    ]
    public void Parse_DashStyle_Mapped(string value, LineDashStyle expected)
    {
        var ln = new XElement(
            DmlNames.Line,
            new XElement(DmlNames.PresetDash, new XAttribute(DmlNames.AttributeValue, value))
        );
        var parent = new XElement(DmlNames.Dml + "spPr", ln);
        var line = new LineFormat();
        LineParser.Parse(parent, line);
        line.DashStyle.ShouldBe(expected);
    }

    [
        Theory,
        InlineData("triangle", ArrowHeadType.Triangle),
        InlineData("stealth", ArrowHeadType.Stealth),
        InlineData("diamond", ArrowHeadType.Diamond),
        InlineData("oval", ArrowHeadType.Oval),
        InlineData("arrow", ArrowHeadType.Open),
        InlineData("open", ArrowHeadType.Open),
        InlineData("none", ArrowHeadType.None),
        InlineData("unknown", ArrowHeadType.None)
    ]
    public void Parse_ArrowType_Mapped(string value, ArrowHeadType expected)
    {
        var ln = new XElement(
            DmlNames.Line,
            new XElement(DmlNames.HeadEnd, new XAttribute("type", value))
        );
        var parent = new XElement(DmlNames.Dml + "spPr", ln);
        var line = new LineFormat();
        LineParser.Parse(parent, line);
        line.HeadArrow.HeadType.ShouldBe(expected);
    }

    [
        Theory,
        InlineData("sm", ArrowHeadSize.Small),
        InlineData("lg", ArrowHeadSize.Large),
        InlineData("med", ArrowHeadSize.Medium),
        InlineData("unknown", ArrowHeadSize.Medium)
    ]
    public void Parse_ArrowSize_Mapped(string value, ArrowHeadSize expected)
    {
        var ln = new XElement(
            DmlNames.Line,
            new XElement(DmlNames.HeadEnd, new XAttribute("type", "triangle"), new XAttribute("w", value))
        );
        var parent = new XElement(DmlNames.Dml + "spPr", ln);
        var line = new LineFormat();
        LineParser.Parse(parent, line);
        line.HeadArrow.Width.ShouldBe(expected);
    }

    [Fact]
    public void Parse_HeadAndTailArrows_Mapped()
    {
        var ln = new XElement(
            DmlNames.Line,
            new XElement(
                DmlNames.HeadEnd,
                new XAttribute("type", "triangle"),
                new XAttribute("w", "lg"),
                new XAttribute("len", "sm")
            ),
            new XElement(
                DmlNames.TailEnd,
                new XAttribute("type", "stealth"),
                new XAttribute("w", "med"),
                new XAttribute("len", "lg")
            )
        );
        var parent = new XElement(DmlNames.Dml + "spPr", ln);
        var line = new LineFormat();
        LineParser.Parse(parent, line);

        line.HeadArrow.HeadType.ShouldBe(ArrowHeadType.Triangle);
        line.HeadArrow.Width.ShouldBe(ArrowHeadSize.Large);
        line.HeadArrow.Length.ShouldBe(ArrowHeadSize.Small);
        line.TailArrow.HeadType.ShouldBe(ArrowHeadType.Stealth);
        line.TailArrow.Length.ShouldBe(ArrowHeadSize.Large);
    }

    [Fact]
    public void ParseElement_ReadsWidthAndDashDirectly()
    {
        var lnL = new XElement(
            DmlNames.Dml + "lnL",
            new XAttribute("w", "25400"),
            new XElement(DmlNames.PresetDash, new XAttribute(DmlNames.AttributeValue, "dash"))
        );
        var line = new LineFormat();
        LineParser.ParseElement(lnL, line);
        line.WidthPoints.ShouldNotBeNull();
        line.WidthPoints.Value.ShouldBe(2.0, 0.001);
        line.DashStyle.ShouldBe(LineDashStyle.Dash);
    }

    [Fact]
    public void RoundTrip_WidthAndDash_ThroughWriterAndParser()
    {
        var original = new LineFormat { WidthPoints = 3.0, DashStyle = LineDashStyle.DashDot };
        original.SetSolid(ColorSpec.FromRgb(0, 0, 0), 3.0);

        var parent = WriteLn(original);
        var parsed = new LineFormat();
        LineParser.Parse(parent, parsed);

        parsed.WidthPoints.ShouldNotBeNull();
        parsed.WidthPoints.Value.ShouldBe(3.0, 0.001);
        parsed.DashStyle.ShouldBe(LineDashStyle.DashDot);
    }

    [Fact]
    public void RoundTrip_Arrows_ThroughWriterAndParser()
    {
        var original = new LineFormat
        {
            HeadArrow =
            {
                HeadType = ArrowHeadType.Diamond
            },
            TailArrow =
            {
                HeadType = ArrowHeadType.Oval
            }
        };

        var parent = WriteLn(original);
        var parsed = new LineFormat();
        LineParser.Parse(parent, parsed);

        parsed.HeadArrow.HeadType.ShouldBe(ArrowHeadType.Diamond);
        parsed.TailArrow.HeadType.ShouldBe(ArrowHeadType.Oval);
    }
}
