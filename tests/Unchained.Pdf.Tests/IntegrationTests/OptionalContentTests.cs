using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Tests for optional content groups ("layers", ISO 32000-1 §8.11): reading layers via
/// <see cref="Abstractions.IPdfDocument.GetLayers"/> and toggling their default visibility
/// via <see cref="OptionalContentEditor"/> (the <c>/OCProperties /D /OFF</c> array).
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
        await editor.SetLayerVisibilityAsync(doc, layerOne.ObjectNumber, visible: false, TestContext.Current.CancellationToken);

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.GetLayers().Single(l => l.Name == "Layer One").Visible.ShouldBeFalse();
    }

    [Fact]
    public async Task SetLayerVisibility_ShowHiddenLayer_PersistsAfterReload()
    {
        var editor = new OptionalContentEditor();
        await using var doc = await LoadAsync(PdfFixtures.WithOptionalContentGroups(), TestContext.Current.CancellationToken);

        var layerTwo = doc.GetLayers().Single(l => l.Name == "Layer Two");
        await editor.SetLayerVisibilityAsync(doc, layerTwo.ObjectNumber, visible: true, TestContext.Current.CancellationToken);

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
        await editor.SetLayerVisibilityAsync(doc, layerOne.ObjectNumber, visible: true, TestContext.Current.CancellationToken);

        doc.GetLayers().Single(l => l.Name == "Layer One").Visible.ShouldBeTrue();
    }

    [Fact]
    public async Task SetLayerVisibility_WhenNoOcProperties_Throws()
    {
        var editor = new OptionalContentEditor();
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            () => editor.SetLayerVisibilityAsync(doc, ocgObjectNumber: 5, visible: false, TestContext.Current.CancellationToken));
    }
}
