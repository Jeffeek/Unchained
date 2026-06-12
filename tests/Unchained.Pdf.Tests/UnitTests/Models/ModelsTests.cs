using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class DocumentMetadataTests
{
    [Fact]
    public void Empty_AllFieldsNull()
    {
        var m = DocumentMetadata.Empty;
        m.Title.ShouldBeNull();
        m.Author.ShouldBeNull();
        m.Subject.ShouldBeNull();
        m.Keywords.ShouldBeNull();
        m.Creator.ShouldBeNull();
        m.Producer.ShouldBeNull();
        m.CreationDate.ShouldBeNull();
        m.ModificationDate.ShouldBeNull();
    }

    [Fact]
    public void Empty_IsSingleton() =>
        DocumentMetadata.Empty.ShouldBeSameAs(DocumentMetadata.Empty);

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new DocumentMetadata(
            "T",
            "A",
            null,
            null,
            null,
            null,
            null,
            null
        );
        var b = new DocumentMetadata(
            "T",
            "A",
            null,
            null,
            null,
            null,
            null,
            null
        );
        a.ShouldBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_NotEqual()
    {
        var a = new DocumentMetadata(
            "T1",
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        var b = new DocumentMetadata(
            "T2",
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = DocumentMetadata.Empty;
        var modified = original with { Title = "My Doc" };
        modified.Title.ShouldBe("My Doc");
        original.Title.ShouldBeNull();
    }
}

public sealed class SaveOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        SaveOptions.Default.Version.ShouldBe(PdfVersion.Pdf17);
        SaveOptions.Default.Linearize.ShouldBeFalse();
        SaveOptions.Default.OptimizeImages.ShouldBeFalse();
    }

    [Fact]
    public void WebOptimized_IsLinearized() =>
        SaveOptions.WebOptimized.Linearize.ShouldBeTrue();

    [Fact]
    public void WebOptimized_VersionIsPdf17() =>
        SaveOptions.WebOptimized.Version.ShouldBe(PdfVersion.Pdf17);

    [Fact]
    public void RecordEquality_Works()
    {
        // ReSharper disable RedundantArgumentDefaultValue
        var a = new SaveOptions(PdfVersion.Pdf17, false);
        var b = new SaveOptions(PdfVersion.Pdf17, false);
        // ReSharper restore RedundantArgumentDefaultValue
        a.ShouldBe(b);
    }
}

public sealed class MergeOptionsTests
{
    [Fact]
    public void Default_CopiesOutlinesAndDestinations()
    {
        MergeOptions.Default.CopyOutlines.ShouldBeTrue();
        MergeOptions.Default.CopyNamedDestinations.ShouldBeTrue();
        MergeOptions.Default.OptimizeResources.ShouldBeFalse();
    }

    [Fact]
    public void Fast_SkipsOutlinesAndDestinations()
    {
        MergeOptions.Fast.CopyOutlines.ShouldBeFalse();
        MergeOptions.Fast.CopyNamedDestinations.ShouldBeFalse();
    }
}

public sealed class TableDataTests
{
    [Fact]
    public void Properties_StoredCorrectly()
    {
        var data = new TableData
        {
            Headers = ["Name", "Age"],
            Rows = [["Alice", "30"], ["Bob", "25"]],
            Title = "People"
        };
        data.Headers.Count.ShouldBe(2);
        data.Rows.Count.ShouldBe(2);
        data.Title.ShouldBe("People");
    }

    [Fact]
    public void Title_NullByDefault()
    {
        var data = new TableData { Headers = [], Rows = [] };
        data.Title.ShouldBeNull();
    }
}

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

public sealed class PdfVersionTests
{
    [Fact]
    public void EnumValues_AllDefined()
    {
        Enum.IsDefined(PdfVersion.Pdf14).ShouldBeTrue();
        Enum.IsDefined(PdfVersion.Pdf15).ShouldBeTrue();
        Enum.IsDefined(PdfVersion.Pdf17).ShouldBeTrue();
        Enum.IsDefined(PdfVersion.PdfA1b).ShouldBeTrue();
        Enum.IsDefined(PdfVersion.PdfA2b).ShouldBeTrue();
    }
}
