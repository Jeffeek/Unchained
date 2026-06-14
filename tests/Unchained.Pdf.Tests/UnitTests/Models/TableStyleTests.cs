using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class TableStyleTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        TableStyle.Default.FontName.ShouldBe("Helvetica");
        TableStyle.Default.HeaderFontSize.ShouldBe(10f);
        TableStyle.Default.CellFontSize.ShouldBe(9f);
        TableStyle.Default.CellPaddingPt.ShouldBe(4f);
        TableStyle.Default.AlternatingRowColor.ShouldBeTrue();
        TableStyle.Default.DrawBorders.ShouldBeTrue();
    }

    [Fact]
    public void Compact_HasSmallerFontsAndPadding()
    {
        TableStyle.Compact.HeaderFontSize.ShouldBeLessThan(TableStyle.Default.HeaderFontSize);
        TableStyle.Compact.CellFontSize.ShouldBeLessThan(TableStyle.Default.CellFontSize);
        TableStyle.Compact.CellPaddingPt.ShouldBeLessThan(TableStyle.Default.CellPaddingPt);
    }
}
