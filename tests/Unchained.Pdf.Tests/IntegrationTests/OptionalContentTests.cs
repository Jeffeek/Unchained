using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Tests for optional content groups ("layers", ISO 32000-1 §8.11): reading layers via
///     <see cref="Abstractions.IPdfDocument.GetLayers" /> and toggling their default visibility
///     via <see cref="OptionalContentEditor" /> (the <c>/OCProperties /D /OFF</c> array).
/// </summary>
public sealed class OptionalContentTests : PdfTestBase
{
    // ── Read ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLayers_WhenNoneExist_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.GetLayers().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLayers_ReturnsAllLayersWithNames()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithOptionalContentGroups(), TestContext.Current.CancellationToken);

        var layers = doc.GetLayers();
        layers.Count.ShouldBe(2);
        layers.Select(l => l.Name).ShouldBe(["Layer One", "Layer Two"]);
    }

    [Fact]
    public async Task GetLayers_ReportsDefaultVisibility()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithOptionalContentGroups(), TestContext.Current.CancellationToken);

        var layers = doc.GetLayers();
        // Layer One is not in /OFF → visible; Layer Two is in /OFF → hidden.
        layers.Single(l => l.Name == "Layer One").Visible.ShouldBeTrue();
        layers.Single(l => l.Name == "Layer Two").Visible.ShouldBeFalse();
    }

    // ── Toggle ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLayersAsync_MatchesSyncResult()
    {
        var editor = new OptionalContentEditor();
        await using var doc = await LoadAsync(PdfFixtures.WithOptionalContentGroups(), TestContext.Current.CancellationToken);

        var layers = await editor.GetLayersAsync(doc, TestContext.Current.CancellationToken);
        layers.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SetLayerVisibility_HideVisibleLayer_PersistsAfterReload()
    {
        var editor = new OptionalContentEditor();
        await using var doc = await LoadAsync(PdfFixtures.WithOptionalContentGroups(), TestContext.Current.CancellationToken);

        var layerOne = doc.GetLayers().Single(l => l.Name == "Layer One");
        await editor.SetLayerVisibilityAsync(doc, layerOne.ObjectNumber, false, TestContext.Current.CancellationToken);

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.GetLayers().Single(l => l.Name == "Layer One").Visible.ShouldBeFalse();
    }

    [Fact]
    public async Task SetLayerVisibility_ShowHiddenLayer_PersistsAfterReload()
    {
        var editor = new OptionalContentEditor();
        await using var doc = await LoadAsync(PdfFixtures.WithOptionalContentGroups(), TestContext.Current.CancellationToken);

        var layerTwo = doc.GetLayers().Single(l => l.Name == "Layer Two");
        await editor.SetLayerVisibilityAsync(doc, layerTwo.ObjectNumber, true, TestContext.Current.CancellationToken);

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.GetLayers().Single(l => l.Name == "Layer Two").Visible.ShouldBeTrue();
    }

    [Fact]
    public async Task SetLayerVisibility_NoOpWhenAlreadyInDesiredState_StaysLoadable()
    {
        var editor = new OptionalContentEditor();
        await using var doc = await LoadAsync(PdfFixtures.WithOptionalContentGroups(), TestContext.Current.CancellationToken);

        // Layer One is already visible — request visible again (no change path).
        var layerOne = doc.GetLayers().Single(l => l.Name == "Layer One");
        await editor.SetLayerVisibilityAsync(doc, layerOne.ObjectNumber, true, TestContext.Current.CancellationToken);

        doc.GetLayers().Single(l => l.Name == "Layer One").Visible.ShouldBeTrue();
    }

    [Fact]
    public async Task SetLayerVisibility_WhenNoOcProperties_Throws()
    {
        var editor = new OptionalContentEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            editor.SetLayerVisibilityAsync(doc, 5, false, TestContext.Current.CancellationToken));
    }

    // ── Soft mask parsing ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSoftMasks_WithSoftMaskFixture_ReturnsEntry()
    {
        await using var doc = await LoadAsync(
            PdfFixtures.WithSoftMask(),
            TestContext.Current.CancellationToken);

        var page = doc.Pages[1];
        var pixW = (int)(page.Width * 72.0 / 72.0);
        var pixH = (int)(page.Height * 72.0 / 72.0);
        var softMasks = page.GetSoftMasks(pixW, pixH);

        softMasks.ShouldNotBeEmpty("expected GS1 soft mask entry to be parsed");
        softMasks.ContainsKey("GS1").ShouldBeTrue("soft mask should be keyed by ExtGState name GS1");

        var sm = softMasks["GS1"];
        sm.WidthPx.ShouldBe(pixW);
        sm.HeightPx.ShouldBe(pixH);
        sm.MaskType.ShouldBe("Alpha");
        sm.Operators.ShouldNotBeEmpty("mask form should have content operators");
    }

    [Fact]
    public async Task GetExtGStateAlphas_WithSoftMaskFixture_IncludesSoftMaskName()
    {
        await using var doc = await LoadAsync(
            PdfFixtures.WithSoftMask(),
            TestContext.Current.CancellationToken);

        var alphas = doc.Pages[1].GetExtGStateAlphas();

        alphas.ContainsKey("GS1").ShouldBeTrue("GS1 ExtGState should be present");
        alphas["GS1"].SoftMaskName.ShouldBe("GS1", "soft mask name should match ExtGState key");
    }
}
