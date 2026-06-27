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
    public async Task GetNamedDestinations_LegacyDestsDict_ReturnsEntry()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithLegacyDests(), TestContext.Current.CancellationToken);
        var dests = doc.GetNamedDestinations();
        dests.ShouldContain(static d => d.Name == "intro");
    }

    [Fact]
    public async Task GetNamedDestinations_NameTreeWithKids_TraversesIntermediateNodes()
    {
        // /Names /Dests root with /Kids → leaf nodes carrying /Names pairs. Exercises the Kids
        // recursion (416-423) and a GoTo-action-dict destination (454-456).
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /Names << /Dests << /Kids [5 0 R] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>",
            "<< /Names [(viaArray) [3 0 R /Fit] (viaAction) 6 0 R] >>",
            "<< /Type /Action /S /GoTo /Dest [4 0 R /Fit] >>"
        };
        await using var doc = await LoadAsync(RawPdfBuilder.Build(bodies), TestContext.Current.CancellationToken);
        var dests = doc.GetNamedDestinations();
        dests.ShouldContain(static d => d.Name == "viaArray" && d.PageNumber == 1);
        dests.ShouldContain(static d => d.Name == "viaAction" && d.PageNumber == 2);
    }

    [Fact]
    public async Task GetNamedDestinations_UnresolvableEntries_AreSkipped()
    {
        // A leaf whose dest entries are unresolvable: a non-array/non-GoTo value, a GoTo action
        // with no /Dest, and a dest array whose first element is not a page reference. All yield
        // pageNum 0 and are dropped (exercises the ResolveDestPageFromObject reject branches).
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /Names << /Dests << /Names [(bare) 5 0 R (noDest) 6 0 R (notRef) [99 /Fit]] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>",
            "<< /Foo 1 >>",
            "<< /Type /Annot >>",          // bare: dict that is not a GoTo action
            "<< /Type /Action /S /GoTo >>" // noDest: GoTo action with no /Dest
        };
        await using var doc = await LoadAsync(RawPdfBuilder.Build(bodies), TestContext.Current.CancellationToken);
        // None of the three entries resolve to a page → all skipped.
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
