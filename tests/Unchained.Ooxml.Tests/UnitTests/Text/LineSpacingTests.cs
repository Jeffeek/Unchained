using Shouldly;
using Unchained.Ooxml.Text;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Text;

public sealed class LineSpacingTests
{
    [Fact]
    public void FromPoints_SetsValueAndMode()
    {
        var spacing = LineSpacing.FromPoints(18);
        spacing.Value.ShouldBe(18);
        spacing.Mode.ShouldBe(LineSpacingMode.Points);
    }

    [Fact]
    public void FromPercent_SetsValueAndMode()
    {
        var spacing = LineSpacing.FromPercent(150);
        spacing.Value.ShouldBe(150);
        spacing.Mode.ShouldBe(LineSpacingMode.Percent);
    }

    [Fact]
    public void Single_Is100Percent()
    {
        LineSpacing.Single.Value.ShouldBe(100);
        LineSpacing.Single.Mode.ShouldBe(LineSpacingMode.Percent);
    }

    [Fact]
    public void OneAndAHalf_Is150Percent()
    {
        LineSpacing.OneAndAHalf.Value.ShouldBe(150);
        LineSpacing.OneAndAHalf.Mode.ShouldBe(LineSpacingMode.Percent);
    }

    [Fact]
    public void Double_Is200Percent()
    {
        LineSpacing.Double.Value.ShouldBe(200);
        LineSpacing.Double.Mode.ShouldBe(LineSpacingMode.Percent);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        LineSpacing.FromPoints(12).ShouldBe(LineSpacing.FromPoints(12));
        LineSpacing.FromPercent(100).ShouldBe(LineSpacing.Single);
    }

    [Fact]
    public void RecordEquality_DifferentMode_AreNotEqual() =>
        LineSpacing.FromPoints(100).ShouldNotBe(LineSpacing.FromPercent(100));
}
