using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests for <see cref="Engine.PageLabelEditor" /> — reading and writing
///     the PDF <c>/PageLabels</c> number tree (ISO 32000-1 §12.4.2).
/// </summary>
public sealed class PageLabelsTests : PdfTestBase
{
    [Fact]
    public async Task GetPageLabels_WhenNotSet_ReturnsEmpty()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        editor.GetPageLabels(doc).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetPageLabels_TwoRanges_RoundTripsCorrectly()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(5), TestContext.Current.CancellationToken);

        var ranges = new List<PageLabelRange>
        {
            new(0, PageLabelStyle.RomanLower),
            new(2)
        };
        await editor.SetPageLabelsAsync(doc, ranges, TestContext.Current.CancellationToken);

        var result = editor.GetPageLabels(doc);
        result.Count.ShouldBe(2);
        result[0].Style.ShouldBe(PageLabelStyle.RomanLower);
        result[1].Style.ShouldBe(PageLabelStyle.Decimal);
        result[1].StartPageIndex.ShouldBe(2);
    }

    [Fact]
    public async Task SetPageLabels_WithPrefix_RoundTripsPrefix()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);

        await editor.SetPageLabelsAsync(
            doc,
            [new PageLabelRange(0, PageLabelStyle.Decimal, "A-")],
            TestContext.Current.CancellationToken
        );

        var result = editor.GetPageLabels(doc);
        result[0].Prefix.ShouldBe("A-");
    }

    [Fact]
    public async Task RemovePageLabels_AfterSet_ReturnsEmpty()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);

        await editor.SetPageLabelsAsync(
            doc,
            [new PageLabelRange(0)],
            TestContext.Current.CancellationToken
        );

        await editor.RemovePageLabelsAsync(doc, TestContext.Current.CancellationToken);
        editor.GetPageLabels(doc).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetPageLabels_FirstRangeNotAtZero_Throws()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentException>(() =>
            editor.SetPageLabelsAsync(
                doc,
                [new PageLabelRange(1)],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task SetPageLabels_SaveAndReload_Persists()
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(4), TestContext.Current.CancellationToken);

        await editor.SetPageLabelsAsync(
            doc,
            [new PageLabelRange(0, PageLabelStyle.AlphaUpper)],
            TestContext.Current.CancellationToken
        );

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        var result = editor.GetPageLabels(reloaded);
        result.Count.ShouldBe(1);
        result[0].Style.ShouldBe(PageLabelStyle.AlphaUpper);
    }

    [
        Theory,
        InlineData(PageLabelStyle.Decimal, "D"),
        InlineData(PageLabelStyle.RomanUpper, "R"),
        InlineData(PageLabelStyle.RomanLower, "r"),
        InlineData(PageLabelStyle.AlphaUpper, "A"),
        InlineData(PageLabelStyle.AlphaLower, "a")
    ]
    public async Task SetPageLabels_AllStyles_RoundTrip(PageLabelStyle style, string _)
    {
        var editor = new PageLabelEditor();
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);

        await editor.SetPageLabelsAsync(
            doc,
            [new PageLabelRange(0, style)],
            TestContext.Current.CancellationToken
        );

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        editor.GetPageLabels(reloaded)[0].Style.ShouldBe(style);
    }

    [Fact]
    public async Task GetPageLabels_NumberTreeWithKids_TraversesIntermediateNodes()
    {
        // A /PageLabels number tree whose root has /Kids pointing at leaf nodes with /Nums.
        // The flat writer never emits this shape, so a hand-built PDF exercises the Kids branch.
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /PageLabels << /Kids [5 0 R 6 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>",
            "<< /Nums [0 << /S /r >>] >>",
            "<< /Nums [1 << /S /D /St 5 >>] >>"
        };
        await using var doc = await LoadAsync(RawPdfBuilder.Build(bodies), TestContext.Current.CancellationToken);
        var labels = new PageLabelEditor().GetPageLabels(doc);
        labels.Count.ShouldBe(2);
        labels[0].Style.ShouldBe(PageLabelStyle.RomanLower);
        labels[1].Style.ShouldBe(PageLabelStyle.Decimal);
        labels[1].FirstLabelNumber.ShouldBe(5);
    }
}
