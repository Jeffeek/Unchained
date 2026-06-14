using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
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

    // ── Proportional width computation from data ──────────────────────────────

    // The uncovered branch is `total < usableWidth` — distribute remaining space evenly.
    // This fires when column content is narrow enough that the sum is less than usableWidth.

    [Fact]
    public void Compute_WithShortData_ColumnWidthsSumEqualsUsableWidth()
    {
        // Short cell text → raw sum < usableWidth → extra space distributed evenly.
        var data = new TableData
        {
            Headers = ["A", "B", "C"],
            Rows = [["1", "2", "3"], ["4", "5", "6"]]
        };
        var layout = TableLayout.Compute(3, DefaultStyle, false, data);
        const float usable = TableLayout.PageWidth - (2 * TableLayout.Margin);
        layout.ColumnWidths.Sum().ShouldBe(usable, 0.1f);
    }

    [Fact]
    public void Compute_WithShortData_SingleColumn_WidthEqualsUsable()
    {
        var data = new TableData
        {
            Headers = ["X"],
            Rows = [["tiny"]]
        };
        var layout = TableLayout.Compute(1, DefaultStyle, false, data);
        const float usable = TableLayout.PageWidth - (2 * TableLayout.Margin);
        layout.ColumnWidths[0].ShouldBe(usable, 0.1f);
    }

    [Fact]
    public void Compute_WithVeryLongText_ScalesDownProportionally()
    {
        // Long header text forces total > usableWidth → scale-down branch.
        var longHeader = new string('W', 200); // very wide text
        var data = new TableData
        {
            Headers = [longHeader, longHeader, longHeader],
            Rows = [[longHeader, longHeader, longHeader]]
        };
        var layout = TableLayout.Compute(3, DefaultStyle, false, data);
        const float usable = TableLayout.PageWidth - (2 * TableLayout.Margin);
        layout.ColumnWidths.Sum().ShouldBe(usable, 0.5f);
    }

    [Fact]
    public void Compute_WithData_ColumnCountMatchesHeaders()
    {
        var data = new TableData
        {
            Headers = ["Name", "Age", "City", "Country"],
            Rows = [["Alice", "30", "Berlin", "Germany"]]
        };
        var layout = TableLayout.Compute(4, DefaultStyle, false, data);
        layout.ColumnWidths.Length.ShouldBe(4);
    }

    [Fact]
    public void Compute_WithData_HasTitle_TitleHeightNonZero()
    {
        var data = new TableData
        {
            Headers = ["Col1", "Col2"],
            Rows = [["a", "b"]],
            Title = "My Table"
        };
        var layout = TableLayout.Compute(2, DefaultStyle, true, data);
        layout.TitleHeight.ShouldBeGreaterThan(0f);
    }

    [Fact]
    public void Compute_WithSingleRow_ColumnWidthsSumEqualsUsable()
    {
        var data = new TableData
        {
            Headers = ["First", "Second"],
            Rows = [["val1", "val2"]]
        };
        var layout = TableLayout.Compute(2, DefaultStyle, false, data);
        const float usable = TableLayout.PageWidth - (2 * TableLayout.Margin);
        layout.ColumnWidths.Sum().ShouldBe(usable, 0.1f);
    }

    [Fact]
    public void Compute_WithEmptyRows_ColumnWidthsDrivenByHeaders()
    {
        // No data rows — widths determined only by header text.
        var data = new TableData
        {
            Headers = ["Header1", "Header2", "Header3"],
            Rows = []
        };
        var layout = TableLayout.Compute(3, DefaultStyle, false, data);
        const float usable = TableLayout.PageWidth - (2 * TableLayout.Margin);
        layout.ColumnWidths.Sum().ShouldBe(usable, 0.1f);
    }

    [Fact]
    public void Compute_WithData_WideAndNarrowColumns_AllPositive()
    {
        // Ensures proportional scaling never produces a zero or negative width.
        var data = new TableData
        {
            Headers = ["ID", "Description with very long text that dominates width"],
            Rows = [["1", "Short"]]
        };
        var layout = TableLayout.Compute(2, DefaultStyle, false, data);
        foreach (var w in layout.ColumnWidths)
            w.ShouldBeGreaterThan(0f);
    }

    [Fact]
    public void Compute_WithData_RowsPerPage_AtLeastOne()
    {
        var data = PdfFixtures.SimpleTableData(5);
        var layout = TableLayout.Compute(3, DefaultStyle, true, data);
        layout.RowsPerPage.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Compute_NullData_EqualColumnWidths()
    {
        // When data is null columns are equal — exercises the non-proportional branch.
        var layout = TableLayout.Compute(4, DefaultStyle, false);
        const float expected = (TableLayout.PageWidth - (2 * TableLayout.Margin)) / 4;
        foreach (var w in layout.ColumnWidths)
            w.ShouldBe(expected, 0.01f);
    }
}
