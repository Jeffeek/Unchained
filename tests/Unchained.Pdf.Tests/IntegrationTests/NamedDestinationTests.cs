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
}
