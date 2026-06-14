using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class NamedDestinationTests : PdfTestBase
{
    private static readonly NamedDestinationEditor Editor = new();

    [Fact]
    public async Task GetNamedDestinations_NoDests_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        doc.GetNamedDestinations().ShouldBeEmpty();
    }

    [Fact]
    public async Task SetDestination_CanReadBack()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "chapter1", 1, TestContext.Current.CancellationToken);
        var dests = doc.GetNamedDestinations();
        dests.ShouldContain(static d => d.Name == "chapter1" && d.PageNumber == 1);
    }

    [Fact]
    public async Task SetDestination_MultipleNames_AllPresent()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(3), TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "intro", 1, TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "chapter2", 2, TestContext.Current.CancellationToken);
        var dests = doc.GetNamedDestinations();
        dests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveDestination_RemovesEntry()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "temp", 1, TestContext.Current.CancellationToken);
        await Editor.RemoveDestinationAsync(doc, "temp", TestContext.Current.CancellationToken);
        doc.GetNamedDestinations().ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveDestination_NonExistent_NoError()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), TestContext.Current.CancellationToken);
        await Should.NotThrowAsync(() => Editor.RemoveDestinationAsync(doc, "doesNotExist"));
    }

    [Fact]
    public async Task SetDestination_PersistsAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.MultiPage(2), TestContext.Current.CancellationToken);
        await Editor.SetDestinationAsync(doc, "saved-dest", 2, TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, TestContext.Current.CancellationToken);
        reloaded.GetNamedDestinations().ShouldContain(static d => d.Name == "saved-dest" && d.PageNumber == 2);
    }

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
