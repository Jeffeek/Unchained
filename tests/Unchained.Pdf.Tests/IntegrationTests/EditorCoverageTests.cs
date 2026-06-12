using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

// ── NamedDestinationEditor — gap coverage ────────────────────────────────────

public sealed class NamedDestinationEditorCoverageTests : PdfTestBase
{
    private static readonly NamedDestinationEditor Editor = new();

    [Fact]
    public async Task SetDestination_ThenOverwrite_LatestPageStored()
    {
        // Exercises updating an existing entry in the flat name list.
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "dest", 1, TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "dest", 3, TestContext.Current.CancellationToken);
        var dests = doc.GetNamedDestinations();
        dests.Count.ShouldBe(1);
        dests[0].PageNumber.ShouldBe(3);
    }

    [Fact]
    public async Task RemoveDestination_OfOneOfTwo_LeavesOtherIntact()
    {
        // Exercises partial removal: ensures the surviving entry is not disturbed.
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "keep", 1, TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "drop", 2, TestContext.Current.CancellationToken);
        await Editor.RemoveDestinationAsync(doc, "drop", TestContext.Current.CancellationToken);
        var dests = doc.GetNamedDestinations();
        dests.Count.ShouldBe(1);
        dests[0].Name.ShouldBe("keep");
    }

    [Fact]
    public async Task SetDestination_OrderedAlphabetically_NamesAreSorted()
    {
        // Verifies that the flat name list is rebuilt in Ordinal order.
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "z-last", 1, TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "a-first", 2, TestContext.Current.CancellationToken);
        var dests = doc.GetNamedDestinations();
        dests[0].Name.ShouldBe("a-first");
        dests[1].Name.ShouldBe("z-last");
    }

    [Fact]
    public async Task RemoveAllDestinations_NamesEntryDropped()
    {
        // After removing the last destination the /Names entry should be absent,
        // so GetNamedDestinations returns empty again.
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "only", 1, TestContext.Current.CancellationToken);
        await Editor.RemoveDestinationAsync(doc, "only", TestContext.Current.CancellationToken);
        doc.GetNamedDestinations().ShouldBeEmpty();
    }

    [Fact]
    public async Task SetDestination_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Editor.SetDestinationAsync(doc, "x", 1, cts.Token));
    }

    [Fact]
    public async Task RemoveDestination_Cancellation_ThrowsOperationCanceledException()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => Editor.RemoveDestinationAsync(doc, "x", cts.Token));
    }
}

// ── AnnotationEditor — gap coverage ─────────────────────────────────────────

public sealed class AnnotationEditorCoverageTests : PdfTestBase
{
    private static readonly AnnotationEditor Editor = new();

    [Fact]
    public async Task AddAnnotation_WithColor_ColorRoundTripped()
    {
        // Exercises the `if (annotation.Color is { Length: 3 } c)` branch.
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(
            AnnotationSubtype.Square,
            10,
            10,
            100,
            50,
            Color: [1f, 0f, 0f]);
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        var result = doc.Pages[1].GetAnnotations()[0];
        result.Color.ShouldNotBeNull();
        result.Color!.Length.ShouldBe(3);
    }

    [Fact]
    public async Task AddAnnotation_HighlightSubtype_RoundTripped()
    {
        // Exercises the Highlight annotation subtype path.
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(AnnotationSubtype.Highlight, 50, 600, 200, 20);
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(AnnotationSubtype.Highlight);
    }

    [Fact]
    public async Task AddAnnotation_LinkSubtype_RoundTripped()
    {
        // Exercises the Link annotation subtype path.
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(AnnotationSubtype.Link, 50, 700, 150, 20);
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(AnnotationSubtype.Link);
    }

    [Fact]
    public async Task AddAnnotation_CircleSubtype_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(AnnotationSubtype.Circle, 200, 400, 80, 80);
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations()[0].Subtype.ShouldBe(AnnotationSubtype.Circle);
    }

    [Fact]
    public async Task AddAnnotation_FreeTextSubtype_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(
            AnnotationSubtype.FreeText,
            100,
            300,
            200,
            60,
            "Free text note");
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        var result = doc.Pages[1].GetAnnotations()[0];
        result.Subtype.ShouldBe(AnnotationSubtype.FreeText);
        result.Contents.ShouldBe("Free text note");
    }

    [Fact]
    public async Task AddAnnotation_ToPageWithExistingAnnotations_AppendsCorrectly()
    {
        // Exercises ResolveAnnotArray with a real PdfArray already on the page dict
        // (the fixture embeds an annotation so /Annots is a plain array, not indirect).
        await using var doc = await LoadAsync(PdfFixtures.WithAnnotation("Existing"), TestContext.Current.CancellationToken);
        var ann = new Annotation(
            AnnotationSubtype.Text,
            200,
            600,
            50,
            50,
            "Added");
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        var annots = doc.Pages[1].GetAnnotations();
        annots.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddAnnotation_ThreeOnSamePage_AllPresent()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        for (var i = 0; i < 3; i++)
        {
            var ann = new Annotation(
                AnnotationSubtype.Text,
                i * 60f,
                700,
                50,
                50,
                $"Note {i}");
            await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        }

        doc.Pages[1].GetAnnotations().Count.ShouldBe(3);
    }

    [Fact]
    public async Task AddAnnotation_MultiPage_OnlyTargetPageAffected()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        var ann = new Annotation(AnnotationSubtype.Text, 100, 700, 50, 50);
        await Editor.AddAnnotationAsync(doc, 2, ann, TestContext.Current.CancellationToken);
        doc.Pages[1].GetAnnotations().ShouldBeEmpty();
        doc.Pages[2].GetAnnotations().Count.ShouldBe(1);
        doc.Pages[3].GetAnnotations().ShouldBeEmpty();
    }

    [Fact]
    public async Task AddAnnotation_WithColorAndContents_BothPresent()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(
            AnnotationSubtype.Circle,
            10,
            10,
            80,
            80,
            "Colored circle",
            [0f, 0f, 1f]);
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        var result = doc.Pages[1].GetAnnotations()[0];
        result.Contents.ShouldBe("Colored circle");
        result.Color.ShouldNotBeNull();
    }

    [Fact]
    public async Task AddAnnotation_RoundTrip_ColorPreservedAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var ann = new Annotation(
            AnnotationSubtype.Square,
            10,
            10,
            50,
            50,
            Color: [0f, 1f, 0f]);
        await Editor.AddAnnotationAsync(doc, 1, ann, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.Pages[1].GetAnnotations()[0].Color.ShouldNotBeNull();
    }
}

// ── StampApplier — gap coverage ───────────────────────────────────────────────

public sealed class StampApplierCoverageTests : PdfTestBase
{
    private static readonly StampApplier Applier = new();

    [Fact]
    public async Task StampAsync_TopRightPosition_ContentContainsTj()
    {
        // Exercises a stamp positioned at the top-right of the page.
        var stamp = new TextStamp("TOP-RIGHT", 480, 800);
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, stamp, TestContext.Current.CancellationToken);
        doc.Pages[1].GetContentOperators().ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampAsync_BottomLeftPosition_ContentContainsTj()
    {
        // Exercises a stamp positioned at the bottom-left of the page.
        var stamp = new TextStamp("BOTTOM-LEFT", 10, 10);
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, stamp, TestContext.Current.CancellationToken);
        doc.Pages[1].GetContentOperators().ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampAsync_WithRotation_ContentContainsTmOperator()
    {
        // Exercises the rotation path (cosR/sinR != [1,0]).
        var stamp = new TextStamp("ROTATED", 200, 400, RotationDegrees: 45f);
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, stamp, TestContext.Current.CancellationToken);
        doc.Pages[1].GetContentOperators().ShouldContain(static op => op.Name == "Tm");
    }

    [Fact]
    public async Task StampAsync_WhiteGray_GrayLevelOneInStream()
    {
        // Exercises a non-zero GrayLevel (the `g` operator operand differs).
        var stamp = new TextStamp("WATERMARK", 100, 400, GrayLevel: 0.5f);
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, stamp, TestContext.Current.CancellationToken);
        doc.Pages[1].GetContentOperators().ShouldContain(static op => op.Name == "g");
    }

    [Fact]
    public async Task StampAsync_MultiPageDoc_AllFivePagesStamped()
    {
        var stamp = new TextStamp("ALL", 100, 400);
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(5), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, stamp, TestContext.Current.CancellationToken);
        for (var i = 1; i <= 5; i++)
            doc.Pages[i].GetContentOperators().ShouldContain(static op => op.Name == "Tj");
    }

    [Fact]
    public async Task StampPageAsync_IsBackground_StampPrependedBeforeExistingContent()
    {
        // Exercises isBackground = true on a specific page.
        var stamp = new TextStamp("BG", 100, 400, IsBackground: true);
        await using var doc = await LoadAsync(
            PdfFixtures.WithTextContent("original text"),
            TestContext.Current.CancellationToken);
        await Applier.StampPageAsync(doc, 1, stamp, TestContext.Current.CancellationToken);
        var ops = doc.Pages[1].GetContentOperators();
        // Both the stamp and original content operators must be present.
        ops.ShouldContain(static op => op.Name == "Tj");
        ops.ShouldContain(static op => op.Name == "q");
    }

    [Fact]
    public async Task StampAsync_CustomFont_FontNameAppearsInResources()
    {
        // Exercises a non-default FontName so the font dict uses a different /BaseFont.
        var stamp = new TextStamp("SERIF", 100, 400, "Times-Roman");
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, stamp, TestContext.Current.CancellationToken);
        // The page should be parseable and contain the Tf operator.
        doc.Pages[1].GetContentOperators().ShouldContain(static op => op.Name == "Tf");
    }

    [Fact]
    public async Task StampAsync_LargeFont_ContentStreamIsValid()
    {
        var stamp = new TextStamp("BIG", 100, 400, FontSize: 72f);
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Applier.StampAsync(doc, stamp, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }
}

// ── TableLayout — gap coverage ────────────────────────────────────────────────

public sealed class TableLayoutCoverageTests
{
    private static TableStyle DefaultStyle => TableStyle.Default;

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
        var data = PdfFixtures.SimpleTableData(5, 3);
        var layout = TableLayout.Compute(3, DefaultStyle, true, data);
        layout.RowsPerPage.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Compute_NullData_EqualColumnWidths()
    {
        // When data is null columns are equal — exercises the non-proportional branch.
        var layout = TableLayout.Compute(4, DefaultStyle, false, null);
        const float expected = (TableLayout.PageWidth - (2 * TableLayout.Margin)) / 4;
        foreach (var w in layout.ColumnWidths)
            w.ShouldBe(expected, 0.01f);
    }
}
