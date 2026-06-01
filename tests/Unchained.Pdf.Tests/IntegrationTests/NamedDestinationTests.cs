using Shouldly;
using Unchained.Pdf.Engine;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class NamedDestinationTests
{
    private static readonly DocumentProcessor Processor = new();
    private static readonly NamedDestinationEditor Editor = new();

    private static Task<Abstractions.IPdfDocument> LoadAsync(byte[] b) =>
        Processor.LoadAsync(new MemoryStream(b));

    [Fact]
    public async Task GetNamedDestinations_NoDests_ReturnsEmpty()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        doc.GetNamedDestinations().ShouldBeEmpty();
    }

    [Fact]
    public async Task SetDestination_CanReadBack()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.MultiPage(count: 2));
        await Editor.SetDestinationAsync(doc, "chapter1", pageNumber: 1);
        var dests = doc.GetNamedDestinations();
        dests.ShouldContain(static d => d.Name == "chapter1" && d.PageNumber == 1);
    }

    [Fact]
    public async Task SetDestination_MultipleNames_AllPresent()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.MultiPage(count: 3));
        await Editor.SetDestinationAsync(doc, "intro", pageNumber: 1);
        await Editor.SetDestinationAsync(doc, "chapter2", pageNumber: 2);
        var dests = doc.GetNamedDestinations();
        dests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveDestination_RemovesEntry()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.MultiPage(count: 2));
        await Editor.SetDestinationAsync(doc, "temp", pageNumber: 1);
        await Editor.RemoveDestinationAsync(doc, "temp");
        doc.GetNamedDestinations().ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveDestination_NonExistent_NoError()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        await Should.NotThrowAsync(() => Editor.RemoveDestinationAsync(doc, "doesNotExist"));
    }

    [Fact]
    public async Task SetDestination_PersistsAfterSave()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.MultiPage(count: 2));
        await Editor.SetDestinationAsync(doc, "saved-dest", pageNumber: 2);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        ms.Position = 0;
        await using var reloaded = await Processor.LoadAsync(ms);
        reloaded.GetNamedDestinations().ShouldContain(static d => d.Name == "saved-dest" && d.PageNumber == 2);
    }
}
