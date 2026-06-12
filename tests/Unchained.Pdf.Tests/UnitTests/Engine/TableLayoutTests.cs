using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

public sealed class TableLayoutTests
{
    private static TableStyle DefaultStyle => TableStyle.Default;

    [Fact]
    public void Compute_ColumnWidths_SumEqualsUsableWidth()
    {
        var layout = TableLayout.Compute(4, DefaultStyle, false);
        layout.ColumnWidths.Sum().ShouldBe(TableLayout.PageWidth - (2 * TableLayout.Margin), 0.01f);
    }

    [Fact]
    public void Compute_EqualColumnWidths_AllSame()
    {
        const int cols = 3;
        var layout = TableLayout.Compute(cols, DefaultStyle, false);
        const float expected = (TableLayout.PageWidth - (2 * TableLayout.Margin)) / cols;
        foreach (var w in layout.ColumnWidths)
            w.ShouldBe(expected, 0.01f);
    }

    [Fact]
    public void Compute_RowHeight_IsDoublePaddingPlusFontSize()
    {
        var style = TableStyle.Default;
        var layout = TableLayout.Compute(2, style, false);
        layout.RowHeight.ShouldBe((2 * style.CellPaddingPt) + style.CellFontSize, 0.001f);
    }

    [Fact]
    public void Compute_HeaderRowHeight_IsDoublePaddingPlusHeaderFontSize()
    {
        var style = TableStyle.Default;
        var layout = TableLayout.Compute(2, style, false);
        layout.HeaderRowHeight.ShouldBe((2 * style.CellPaddingPt) + style.HeaderFontSize, 0.001f);
    }

    [Fact]
    public void Compute_TitleHeight_ZeroWhenNoTitle()
    {
        var layout = TableLayout.Compute(2, DefaultStyle, false);
        layout.TitleHeight.ShouldBe(0f);
    }

    [Fact]
    public void Compute_TitleHeight_NonZeroWhenHasTitle()
    {
        var layout = TableLayout.Compute(2, DefaultStyle, true);
        layout.TitleHeight.ShouldBeGreaterThan(0f);
    }

    [Fact]
    public void Compute_TableWidth_EqualsUsableWidth()
    {
        var layout = TableLayout.Compute(5, DefaultStyle, false);
        layout.TableWidth.ShouldBe(TableLayout.PageWidth - (2 * TableLayout.Margin), 0.01f);
    }

    [Fact]
    public void Compute_RowsPerPage_FitsInUsableHeight()
    {
        var layout = TableLayout.Compute(2, DefaultStyle, false);
        var usable = TableLayout.PageHeight - (2 * TableLayout.Margin) - layout.TitleHeight - layout.HeaderRowHeight;
        var expected = (int)(usable / layout.RowHeight);
        layout.RowsPerPage.ShouldBe(expected);
    }

    [Fact]
    public void Compute_RowsPerPage_AtLeastOne()
    {
        var layout = TableLayout.Compute(2, DefaultStyle, false);
        layout.RowsPerPage.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Compute_ManyColumns_ColumnCountMatches()
    {
        var layout = TableLayout.Compute(20, DefaultStyle, false);
        layout.ColumnWidths.Length.ShouldBe(20);
    }

    [Fact]
    public void Compute_SingleColumn_WidthEqualsFullUsableWidth()
    {
        var layout = TableLayout.Compute(1, DefaultStyle, false);
        layout.ColumnWidths[0].ShouldBe(TableLayout.PageWidth - (2 * TableLayout.Margin), 0.01f);
    }
}
