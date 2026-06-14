using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Writing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

public sealed class LineWriterTests
{
    private static XElement WriteLine(LineFormat line)
    {
        var parent = new XElement("parent");
        LineWriter.Write(parent, line);
        return parent.Elements().Single(static e => e.Name.LocalName == "ln");
    }

    [Fact]
    public void Write_DefaultLine_ProducesLnElement()
    {
        var ln = WriteLine(new LineFormat());
        ln.Name.LocalName.ShouldBe("ln");
        // Default fill is None → noFill child.
        ln.Elements().Any(static e => e.Name.LocalName == "noFill").ShouldBeTrue();
    }

    [Fact]
    public void Write_WidthInPoints_ConvertedToEmuAttribute()
    {
        var line = new LineFormat { WidthPoints = 1.0 };
        var ln = WriteLine(line);
        // 1 pt = 12700 EMU.
        ln.Attribute("w")!.Value.ShouldBe("12700");
    }

    [Fact]
    public void Write_SolidLine_EmitsSolidFill()
    {
        var line = new LineFormat();
        line.SetSolid(ColorSpec.FromRgb(0, 0, 0), 2.0);
        var ln = WriteLine(line);
        ln.Elements().Any(static e => e.Name.LocalName == "solidFill").ShouldBeTrue();
    }

    [
        Theory,
        InlineData(LineDashStyle.Dot, "dot"),
        InlineData(LineDashStyle.Dash, "dash"),
        InlineData(LineDashStyle.DashDot, "dashDot"),
        InlineData(LineDashStyle.LongDash, "lgDash"),
        InlineData(LineDashStyle.SystemDash, "sysDash")
    ]
    public void Write_DashStyle_EmitsPresetDash(LineDashStyle style, string expected)
    {
        var line = new LineFormat { DashStyle = style };
        var ln = WriteLine(line);
        var dash = ln.Elements().Single(static e => e.Name.LocalName == "prstDash");
        dash.Attribute("val")!.Value.ShouldBe(expected);
    }

    [Fact]
    public void Write_SolidDash_OmitsPresetDash()
    {
        var ln = WriteLine(new LineFormat { DashStyle = LineDashStyle.Solid });
        ln.Elements().Any(static e => e.Name.LocalName == "prstDash").ShouldBeFalse();
    }

    [Fact]
    public void Write_HeadArrow_EmitsHeadEnd()
    {
        var line = new LineFormat
        {
            HeadArrow =
            {
                HeadType = ArrowHeadType.Triangle
            }
        };
        var ln = WriteLine(line);
        var head = ln.Elements().Single(static e => e.Name.LocalName == "headEnd");
        head.Attribute("type")!.Value.ShouldBe("triangle");
    }

    [Fact]
    public void Write_TailArrow_EmitsTailEndWithSizes()
    {
        var line = new LineFormat
        {
            TailArrow =
            {
                HeadType = ArrowHeadType.Stealth,
                Width = ArrowHeadSize.Large,
                Length = ArrowHeadSize.Small
            }
        };
        var ln = WriteLine(line);
        var tail = ln.Elements().Single(static e => e.Name.LocalName == "tailEnd");
        tail.Attribute("type")!.Value.ShouldBe("stealth");
        tail.Attribute("w")!.Value.ShouldBe("lg");
        tail.Attribute("len")!.Value.ShouldBe("sm");
    }

    [Fact]
    public void Write_NoArrows_OmitsArrowElements()
    {
        var ln = WriteLine(new LineFormat());
        ln.Elements().Any(static e => e.Name.LocalName is "headEnd" or "tailEnd").ShouldBeFalse();
    }
}
