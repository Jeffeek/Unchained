using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class ViewerPreferencesTests : PdfTestBase
{
    private static readonly ViewerPreferencesEditor Editor = new();


    // ── Read ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetViewerPreferences_NoPreferences_ReturnsDefaults()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var prefs = doc.GetViewerPreferences();
        prefs.HideToolbar.ShouldBeFalse();
        prefs.HideMenubar.ShouldBeFalse();
        prefs.CenterWindow.ShouldBeFalse();
        prefs.DisplayDocTitle.ShouldBeFalse();
        prefs.Direction.ShouldBe(ReadingDirection.LeftToRight);
        prefs.Duplex.ShouldBe(DuplexMode.None);
    }

    [Fact]
    public async Task PageLayout_Default_ReturnsDefault()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.PageLayout.ShouldBe(PageLayout.Default);
    }

    [Fact]
    public async Task PageMode_Default_ReturnsDefault()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.PageMode.ShouldBe(PageMode.Default);
    }

    // ── Write + round-trip ────────────────────────────────────────────────────

    [Fact]
    public async Task SetViewerPreferences_HideToolbar_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var prefs = new ViewerPreferences(true);
        await Editor.SetViewerPreferencesAsync(doc, prefs, TestContext.Current.CancellationToken);
        doc.GetViewerPreferences().HideToolbar.ShouldBeTrue();
    }

    [Fact]
    public async Task SetViewerPreferences_CenterWindow_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(doc, new ViewerPreferences(CenterWindow: true), TestContext.Current.CancellationToken);
        doc.GetViewerPreferences().CenterWindow.ShouldBeTrue();
    }

    [Fact]
    public async Task SetViewerPreferences_Direction_RightToLeft_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(
            doc,
            new ViewerPreferences(Direction: ReadingDirection.RightToLeft),
            TestContext.Current.CancellationToken
        );
        doc.GetViewerPreferences().Direction.ShouldBe(ReadingDirection.RightToLeft);
    }

    [Fact]
    public async Task SetPageLayout_TwoColumnLeft_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetPageLayoutAsync(doc, PageLayout.TwoColumnLeft, TestContext.Current.CancellationToken);
        doc.PageLayout.ShouldBe(PageLayout.TwoColumnLeft);
    }

    [Fact]
    public async Task SetPageMode_UseOutlines_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetPageModeAsync(doc, PageMode.UseOutlines, TestContext.Current.CancellationToken);
        doc.PageMode.ShouldBe(PageMode.UseOutlines);
    }

    [Fact]
    public async Task SetViewerPreferences_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(doc, new ViewerPreferences(HideMenubar: true), TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task SetPreferences_SaveAndReload_Persisted()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetPageLayoutAsync(doc, PageLayout.SinglePage, TestContext.Current.CancellationToken);
        await Editor.SetPageModeAsync(doc, PageMode.UseThumbs, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.PageLayout.ShouldBe(PageLayout.SinglePage);
        reloaded.PageMode.ShouldBe(PageMode.UseThumbs);
    }

    [Fact]
    public async Task SetViewerPreferences_HideMenubar_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(doc, new ViewerPreferences(HideMenubar: true), TestContext.Current.CancellationToken);
        doc.GetViewerPreferences().HideMenubar.ShouldBeTrue();
    }

    [Fact]
    public async Task SetViewerPreferences_HideWindowUI_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(doc, new ViewerPreferences(HideWindowUI: true), TestContext.Current.CancellationToken);
        doc.GetViewerPreferences().HideWindowUI.ShouldBeTrue();
    }

    [Fact]
    public async Task SetViewerPreferences_FitWindow_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(doc, new ViewerPreferences(FitWindow: true), TestContext.Current.CancellationToken);
        doc.GetViewerPreferences().FitWindow.ShouldBeTrue();
    }

    [Fact]
    public async Task SetViewerPreferences_DisplayDocTitle_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(doc, new ViewerPreferences(DisplayDocTitle: true), TestContext.Current.CancellationToken);
        doc.GetViewerPreferences().DisplayDocTitle.ShouldBeTrue();
    }

    [Fact]
    public async Task SetViewerPreferences_DuplexSimplex_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(doc, new ViewerPreferences(Duplex: DuplexMode.Simplex), TestContext.Current.CancellationToken);
        doc.GetViewerPreferences().Duplex.ShouldBe(DuplexMode.Simplex);
    }

    [Fact]
    public async Task SetViewerPreferences_DuplexFlipLongEdge_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(
            doc,
            new ViewerPreferences(Duplex: DuplexMode.DuplexFlipLongEdge),
            TestContext.Current.CancellationToken
        );
        doc.GetViewerPreferences().Duplex.ShouldBe(DuplexMode.DuplexFlipLongEdge);
    }

    [Fact]
    public async Task SetViewerPreferences_DuplexFlipShortEdge_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(
            doc,
            new ViewerPreferences(Duplex: DuplexMode.DuplexFlipShortEdge),
            TestContext.Current.CancellationToken
        );
        doc.GetViewerPreferences().Duplex.ShouldBe(DuplexMode.DuplexFlipShortEdge);
    }

    [Fact]
    public async Task SetViewerPreferences_NonFullScreenPageMode_UseOutlines_RoundTripped()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetViewerPreferencesAsync(
            doc,
            new ViewerPreferences(NonFullScreenPageMode: PageMode.UseOutlines),
            TestContext.Current.CancellationToken
        );
        doc.GetViewerPreferences().NonFullScreenPageMode.ShouldBe(PageMode.UseOutlines);
    }

    [
        Theory,
        InlineData(PageLayout.SinglePage),
        InlineData(PageLayout.OneColumn),
        InlineData(PageLayout.TwoColumnLeft),
        InlineData(PageLayout.TwoColumnRight),
        InlineData(PageLayout.TwoPageLeft),
        InlineData(PageLayout.TwoPageRight)
    ]
    public async Task SetPageLayout_AllNonDefaultValues_RoundTripped(PageLayout layout)
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetPageLayoutAsync(doc, layout, TestContext.Current.CancellationToken);
        doc.PageLayout.ShouldBe(layout);
    }

    [
        Theory,
        InlineData(PageMode.UseNone),
        InlineData(PageMode.UseOutlines),
        InlineData(PageMode.UseThumbs),
        InlineData(PageMode.FullScreen),
        InlineData(PageMode.UseOC),
        InlineData(PageMode.UseAttachments)
    ]
    public async Task SetPageMode_AllNonDefaultValues_RoundTripped(PageMode mode)
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetPageModeAsync(doc, mode, TestContext.Current.CancellationToken);
        doc.PageMode.ShouldBe(mode);
    }

    [Fact]
    public async Task SetViewerPreferences_AllFlags_SaveAndReload_Persisted()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        var prefs = new ViewerPreferences(
            true,
            true,
            true,
            true,
            true,
            true,
            ReadingDirection.RightToLeft,
            DuplexMode.Simplex,
            PageMode.UseOutlines
        );
        await Editor.SetViewerPreferencesAsync(doc, prefs, TestContext.Current.CancellationToken);

        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);

        var loaded = reloaded.GetViewerPreferences();
        loaded.HideToolbar.ShouldBeTrue();
        loaded.HideMenubar.ShouldBeTrue();
        loaded.HideWindowUI.ShouldBeTrue();
        loaded.FitWindow.ShouldBeTrue();
        loaded.CenterWindow.ShouldBeTrue();
        loaded.DisplayDocTitle.ShouldBeTrue();
        loaded.Direction.ShouldBe(ReadingDirection.RightToLeft);
        loaded.Duplex.ShouldBe(DuplexMode.Simplex);
        loaded.NonFullScreenPageMode.ShouldBe(PageMode.UseOutlines);
    }

    [Fact]
    public async Task SetPageLayout_Default_DoesNotWritePageLayout()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetPageLayoutAsync(doc, PageLayout.Default, TestContext.Current.CancellationToken);
        doc.PageLayout.ShouldBe(PageLayout.Default);
    }

    [Fact]
    public async Task SetPageMode_Default_DoesNotWritePageMode()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetPageModeAsync(doc, PageMode.Default, TestContext.Current.CancellationToken);
        doc.PageMode.ShouldBe(PageMode.Default);
    }

    [Fact]
    public async Task SetViewerPreferences_NullPrefsObject_NoPrefsKeyWritten()
    {
        // Calling SetPageLayout exercises the Apply path where prefs is null;
        // the /ViewerPreferences key must not appear in the result.
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Editor.SetPageLayoutAsync(doc, PageLayout.TwoPageRight, TestContext.Current.CancellationToken);
        // /ViewerPreferences was never set, so GetViewerPreferences returns defaults.
        var prefs = doc.GetViewerPreferences();
        prefs.HideToolbar.ShouldBeFalse();
        prefs.HideMenubar.ShouldBeFalse();
        doc.PageLayout.ShouldBe(PageLayout.TwoPageRight);
    }
}
