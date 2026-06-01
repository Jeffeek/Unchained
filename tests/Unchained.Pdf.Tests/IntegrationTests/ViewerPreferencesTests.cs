using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class ViewerPreferencesTests
{
    private static readonly DocumentProcessor Processor = new();
    private static readonly ViewerPreferencesEditor Editor = new();

    private static Task<Abstractions.IPdfDocument> LoadAsync(byte[] b) =>
        Processor.LoadAsync(new MemoryStream(b));

    // ── Read ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetViewerPreferences_NoPreferences_ReturnsDefaults()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
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
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        doc.PageLayout.ShouldBe(PageLayout.Default);
    }

    [Fact]
    public async Task PageMode_Default_ReturnsDefault()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        doc.PageMode.ShouldBe(PageMode.Default);
    }

    // ── Write + round-trip ────────────────────────────────────────────────────

    [Fact]
    public async Task SetViewerPreferences_HideToolbar_RoundTripped()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        var prefs = new ViewerPreferences(HideToolbar: true);
        await Editor.SetViewerPreferencesAsync(doc, prefs);
        doc.GetViewerPreferences().HideToolbar.ShouldBeTrue();
    }

    [Fact]
    public async Task SetViewerPreferences_CenterWindow_RoundTripped()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        await Editor.SetViewerPreferencesAsync(doc, new ViewerPreferences(CenterWindow: true));
        doc.GetViewerPreferences().CenterWindow.ShouldBeTrue();
    }

    [Fact]
    public async Task SetViewerPreferences_Direction_RightToLeft_RoundTripped()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        await Editor.SetViewerPreferencesAsync(doc, new ViewerPreferences(Direction: ReadingDirection.RightToLeft));
        doc.GetViewerPreferences().Direction.ShouldBe(ReadingDirection.RightToLeft);
    }

    [Fact]
    public async Task SetPageLayout_TwoColumnLeft_RoundTripped()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        await Editor.SetPageLayoutAsync(doc, PageLayout.TwoColumnLeft);
        doc.PageLayout.ShouldBe(PageLayout.TwoColumnLeft);
    }

    [Fact]
    public async Task SetPageMode_UseOutlines_RoundTripped()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        await Editor.SetPageModeAsync(doc, PageMode.UseOutlines);
        doc.PageMode.ShouldBe(PageMode.UseOutlines);
    }

    [Fact]
    public async Task SetViewerPreferences_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.MultiPage(count: 2));
        await Editor.SetViewerPreferencesAsync(doc, new ViewerPreferences(HideMenubar: true));
        doc.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task SetPreferences_SaveAndReload_Persisted()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        await Editor.SetPageLayoutAsync(doc, PageLayout.SinglePage);
        await Editor.SetPageModeAsync(doc, PageMode.UseThumbs);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        await using var reloaded = await Processor.LoadAsync(ms);
        reloaded.PageLayout.ShouldBe(PageLayout.SinglePage);
        reloaded.PageMode.ShouldBe(PageMode.UseThumbs);
    }
}
