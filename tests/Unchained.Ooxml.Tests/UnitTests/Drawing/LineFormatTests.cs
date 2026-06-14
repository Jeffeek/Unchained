using Shouldly;
using Unchained.Ooxml.Drawing;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Drawing;

public sealed class LineFormatTests
{
    [Fact]
    public void Defaults_AreSolidDashFlatCapMiterJoin()
    {
        var line = new LineFormat();
        line.WidthPoints.ShouldBeNull();
        line.DashStyle.ShouldBe(LineDashStyle.Solid);
        line.CapStyle.ShouldBe(LineCapStyle.Flat);
        line.JoinStyle.ShouldBe(LineJoinStyle.Miter);
        line.Fill.ShouldNotBeNull();
        line.HeadArrow.ShouldNotBeNull();
        line.TailArrow.ShouldNotBeNull();
    }

    [Fact]
    public void SetSolid_SetsWidthAndSolidFill()
    {
        var line = new LineFormat();
        var color = ColorSpec.FromRgb(0x10, 0x20, 0x30);
        line.SetSolid(color, 2.5);

        line.WidthPoints.ShouldBe(2.5);
        line.Fill.Type.ShouldBe(FillType.Solid);
        line.Fill.Solid.ShouldNotBeNull();
        line.Fill.Solid.Color.ShouldBe(color);
    }

    [Fact]
    public void SetSolid_DefaultWidth_IsOnePoint()
    {
        var line = new LineFormat();
        line.SetSolid(ColorSpec.FromRgb(0, 0, 0));
        line.WidthPoints.ShouldBe(1.0);
    }

    [Fact]
    public void SetNone_ClearsFill()
    {
        var line = new LineFormat();
        line.SetSolid(ColorSpec.FromRgb(1, 2, 3));
        line.SetNone();
        line.Fill.Type.ShouldBe(FillType.None);
    }

    [Fact]
    public void Arrows_RoundTripProperties()
    {
        var line = new LineFormat();
        line.HeadArrow.HeadType = ArrowHeadType.Triangle;
        line.TailArrow.HeadType = ArrowHeadType.Stealth;

        line.HeadArrow.HeadType.ShouldBe(ArrowHeadType.Triangle);
        line.TailArrow.HeadType.ShouldBe(ArrowHeadType.Stealth);
    }
}
