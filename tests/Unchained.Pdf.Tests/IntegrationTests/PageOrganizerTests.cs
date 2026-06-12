using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class PageOrganizerTests : PdfTestBase
{
    private static readonly PageOrganizer Organizer = new();

    private static Task<IPdfDocument> LoadFixtureAsync(int pages) =>
        LoadAsync(PdfFixtures.MultiPage(pages));

    // ── Rotate ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RotatePages_Relative90_SetsRotateOnTargetOnly()
    {
        await using var doc = await LoadFixtureAsync(3);
        await Organizer.RotatePagesAsync(doc, [2], 90, true, TestContext.Current.CancellationToken);

        doc.Pages[1].Rotate.ShouldBe(0);
        doc.Pages[2].Rotate.ShouldBe(90);
        doc.Pages[3].Rotate.ShouldBe(0);
        doc.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task RotatePages_RelativeAccumulates_AndNormalises()
    {
        await using var doc = await LoadFixtureAsync(1);
        await Organizer.RotatePagesAsync(doc, [1], 270, ct: TestContext.Current.CancellationToken);
        await Organizer.RotatePagesAsync(doc, [1], 180, ct: TestContext.Current.CancellationToken);
        doc.Pages[1].Rotate.ShouldBe(90); // (270 + 180) % 360
    }

    [Fact]
    public async Task RotatePages_NegativeAngle_Normalises()
    {
        await using var doc = await LoadFixtureAsync(1);
        await Organizer.RotatePagesAsync(doc, [1], -90, ct: TestContext.Current.CancellationToken);
        doc.Pages[1].Rotate.ShouldBe(270);
    }

    [Fact]
    public Task RotatePages_NonMultipleOf90_Throws() =>
        Should.ThrowAsync<ArgumentException>(static async () =>
        {
            await using var doc = await LoadFixtureAsync(1);
            await Organizer.RotatePagesAsync(doc, [1], 45);
        });

    [Fact]
    public async Task RotatePages_SurvivesSaveReload()
    {
        await using var doc = await LoadFixtureAsync(2);
        await Organizer.RotatePagesAsync(doc, [1], 90, ct: TestContext.Current.CancellationToken);
        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.Pages[1].Rotate.ShouldBe(90);
        reloaded.PageCount.ShouldBe(2);
    }

    // ── Delete ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePages_RemovesPages_PageCountDrops()
    {
        await using var doc = await LoadFixtureAsync(5);
        await Organizer.DeletePagesAsync(doc, [2, 4], TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task DeletePages_KeepsRemainingPagesParseable()
    {
        await using var doc = await LoadFixtureAsync(3);
        await Organizer.DeletePagesAsync(doc, [1], TestContext.Current.CancellationToken);
        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(2);
    }

    [Fact]
    public Task DeletePages_AllPages_Throws() =>
        Should.ThrowAsync<ArgumentException>(static async () =>
        {
            await using var doc = await LoadFixtureAsync(2);
            await Organizer.DeletePagesAsync(doc, [1, 2]);
        });

    [Fact]
    public Task DeletePages_OutOfRange_Throws() =>
        Should.ThrowAsync<ArgumentOutOfRangeException>(static async () =>
        {
            await using var doc = await LoadFixtureAsync(2);
            await Organizer.DeletePagesAsync(doc, [3]);
        });

    // ── Reorder ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderPages_Reverse_PageCountUnchanged()
    {
        await using var doc = await LoadFixtureAsync(3);
        await Organizer.ReorderPagesAsync(doc, [3, 2, 1], TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task ReorderPages_AppliesNewOrder_VerifiedByRotationTag()
    {
        // Tag each page with a distinct rotation, then reorder and confirm the tags moved.
        await using var doc = await LoadFixtureAsync(3);
        await Organizer.RotatePagesAsync(doc, [1], 90, false, TestContext.Current.CancellationToken);
        await Organizer.RotatePagesAsync(doc, [2], 180, false, TestContext.Current.CancellationToken);
        await Organizer.RotatePagesAsync(doc, [3], 270, false, TestContext.Current.CancellationToken);

        await Organizer.ReorderPagesAsync(doc, [3, 1, 2], TestContext.Current.CancellationToken);

        doc.Pages[1].Rotate.ShouldBe(270); // was page 3
        doc.Pages[2].Rotate.ShouldBe(90);  // was page 1
        doc.Pages[3].Rotate.ShouldBe(180); // was page 2
    }

    [Fact]
    public Task ReorderPages_NotAPermutation_Throws() =>
        Should.ThrowAsync<ArgumentException>(static async () =>
        {
            await using var doc = await LoadFixtureAsync(3);
            await Organizer.ReorderPagesAsync(doc, [1, 1, 2]);
        });

    // ── Insert ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InsertPages_AtStart_PrependsSourcePages()
    {
        await using var dest = await LoadFixtureAsync(2);
        await using var src = await LoadFixtureAsync(3);
        await Organizer.InsertPagesAsync(dest, 1, src, TestContext.Current.CancellationToken);
        dest.PageCount.ShouldBe(5);
    }

    [Fact]
    public async Task InsertPages_Append_AddsToEnd()
    {
        await using var dest = await LoadFixtureAsync(2);
        await using var src = await LoadFixtureAsync(2);
        await Organizer.InsertPagesAsync(dest, dest.PageCount + 1, src, TestContext.Current.CancellationToken);
        dest.PageCount.ShouldBe(4);
    }

    [Fact]
    public async Task InsertPages_InMiddle_ParseableAfterReload()
    {
        await using var dest = await LoadFixtureAsync(3);
        await using var src = await LoadFixtureAsync(1);
        await Organizer.InsertPagesAsync(dest, 2, src, TestContext.Current.CancellationToken);
        await using var reloaded = await SaveAndReloadAsync(dest, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(4);
    }

    [Fact]
    public Task InsertPages_OutOfRange_Throws() =>
        Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
        {
            await using var dest = await LoadFixtureAsync(2);
            await using var src = await LoadFixtureAsync(1);
            await Organizer.InsertPagesAsync(dest, 5, src);
        });

    // ── Split ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Split_TwoRanges_ProducesTwoDocsWithCorrectCounts()
    {
        await using var doc = await LoadFixtureAsync(5);
        var parts = await Organizer.SplitAsync(doc, [(1, 2), (3, 5)], TestContext.Current.CancellationToken);
        parts.Count.ShouldBe(2);
        parts[0].PageCount.ShouldBe(2);
        parts[1].PageCount.ShouldBe(3);
        foreach (var p in parts) await p.DisposeAsync();
    }

    [Fact]
    public async Task Split_SinglePageRanges_ParseableAfterReload()
    {
        await using var doc = await LoadFixtureAsync(3);
        var parts = await Organizer.SplitAsync(doc, [(1, 1), (2, 2), (3, 3)], TestContext.Current.CancellationToken);
        parts.Count.ShouldBe(3);
        foreach (var p in parts)
        {
            await using var reloaded = await SaveAndReloadAsync(p, TestContext.Current.CancellationToken);
            reloaded.PageCount.ShouldBe(1);
            await p.DisposeAsync();
        }
    }

    [Fact]
    public Task Split_InvalidRange_Throws() =>
        Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
        {
            await using var doc = await LoadFixtureAsync(3);
            await Organizer.SplitAsync(doc, [(2, 1)]);
        });

    [Fact]
    public Task Split_EmptyRanges_Throws() =>
        Should.ThrowAsync<ArgumentException>(async () =>
        {
            await using var doc = await LoadFixtureAsync(3);
            await Organizer.SplitAsync(doc, []);
        });
}
