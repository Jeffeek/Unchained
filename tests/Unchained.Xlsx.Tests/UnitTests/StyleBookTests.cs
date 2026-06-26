using Shouldly;
using Unchained.Xlsx.Models.Styles;
using Unchained.Xlsx.Styles;
using Xunit;

namespace Unchained.Xlsx.Tests.UnitTests;

/// <summary>Direct coverage for <c>StyleBook</c> get-or-add dedup and resolution edges.</summary>
public class StyleBookTests
{
    private static StyleBook Default() => StyleBook.CreateDefault();

    [Fact]
    public void CreateDefault_HasRequiredEntries()
    {
        var book = Default();
        book.Fonts.Count.ShouldBe(1);
        book.Fills.Count.ShouldBe(2);
        book.Fills[0].PatternType.ShouldBe(FillPattern.None);
        book.Fills[1].PatternType.ShouldBe(FillPattern.Gray125);
        book.Borders.Count.ShouldBe(1);
        book.CellXfs.Count.ShouldBe(1);
        book.NamedStyles.Count.ShouldBe(1);
    }

    [Fact]
    public void GetOrAddFont_Deduplicates()
    {
        var book = Default();
        var a = book.GetOrAddFont(new CellFont { Bold = true });
        var b = book.GetOrAddFont(new CellFont { Bold = true });
        a.ShouldBe(b);
    }

    [Fact]
    public void GetOrAddFill_AndBorder_Deduplicate()
    {
        var book = Default();
        var fill = book.GetOrAddFill(CellFill.Solid(Unchained.Ooxml.Drawing.ColorSpec.FromRgb(1, 2, 3)));
        book.GetOrAddFill(CellFill.Solid(Unchained.Ooxml.Drawing.ColorSpec.FromRgb(1, 2, 3))).ShouldBe(fill);

        var border = book.GetOrAddBorder(new CellBorder().SetAllEdges(BorderStyle.Thin));
        book.GetOrAddBorder(new CellBorder().SetAllEdges(BorderStyle.Thin)).ShouldBe(border);
    }

    [Fact]
    public void GetOrAddNumberFormat_BuiltInReused()
    {
        var book = Default();
        // "0.00" is built-in id 2.
        book.GetOrAddNumberFormat("0.00").ShouldBeLessThan(NumberFormat.FirstCustomId);
    }

    [Fact]
    public void GetOrAddNumberFormat_CustomAllocatesId()
    {
        var book = Default();
        var first = book.GetOrAddNumberFormat("0.0000\" units\"");
        first.ShouldBeGreaterThanOrEqualTo(NumberFormat.FirstCustomId);
        // Same code → same id.
        book.GetOrAddNumberFormat("0.0000\" units\"").ShouldBe(first);
    }

    [Fact]
    public void GetOrAddCellXf_Deduplicates()
    {
        var book = Default();
        var xf = new CellXf { FontId = 0, ApplyFont = true };
        var index = book.GetOrAddCellXf(xf);
        book.GetOrAddCellXf(new CellXf { FontId = 0, ApplyFont = true }).ShouldBe(index);
    }

    [Fact]
    public void Resolution_OutOfRange_ReturnsDefaults()
    {
        var book = Default();
        // Index well beyond the table → default CellXf / font / fill / border.
        book.GetCellXf(999).ShouldNotBeNull();
        book.GetFont(999).Name.ShouldBe("Calibri");
        book.GetFill(999).PatternType.ShouldBe(FillPattern.None);
        book.GetBorder(999).Left.Style.ShouldBe(BorderStyle.None);
    }

    [Fact]
    public void GetNumberFormatCode_UnknownId_ReturnsGeneral()
    {
        var book = Default();
        var xfIndex = book.GetOrAddCellXf(new CellXf { NumberFormatId = 9999 });
        book.GetNumberFormatCode(xfIndex).ShouldBe("General");
    }

    [Fact]
    public void GetNumberFormatCode_CustomId_ReturnsCode()
    {
        var book = Default();
        var id = book.GetOrAddNumberFormat("0.000");
        var xfIndex = book.GetOrAddCellXf(new CellXf { NumberFormatId = id });
        book.GetNumberFormatCode(xfIndex).ShouldBe("0.000");
    }

    [Fact]
    public void FindNamedStyle_NormalAndMissing()
    {
        var book = Default();
        book.FindNamedStyle("Normal").ShouldNotBeNull();
        book.FindNamedStyle("nonexistent").ShouldBeNull();
    }

    [Fact]
    public void RebuildLookups_RecoversSparseBook()
    {
        var book = new StyleBook();
        // A book loaded with no tables at all → RebuildLookups fills the minimum required entries.
        book.RebuildLookups();
        book.Fonts.Count.ShouldBeGreaterThanOrEqualTo(1);
        book.Fills.Count.ShouldBeGreaterThanOrEqualTo(2);
        book.Borders.Count.ShouldBeGreaterThanOrEqualTo(1);
        book.CellXfs.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void RebuildLookups_SingleFill_AddsGray125()
    {
        var book = new StyleBook();
        book.AddFillRaw(new CellFill { PatternType = FillPattern.None });
        book.RebuildLookups();
        book.Fills.Count.ShouldBeGreaterThanOrEqualTo(2);
    }
}
