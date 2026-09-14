using Unchained.Pptx.Engine;
using Unchained.Studio.Services;

namespace Unchained.Studio.Tests.Services;

/// <summary>
///     Tests for <see cref="PptxSessionState" /> — create (SDK engine), tree build, refresh
///     (serialize + reload), dirty tracking, and disposal for the PPTX presentation session.
/// </summary>
public sealed class PptxSessionStateTests
{
    private static async Task<byte[]> PresentationBytesAsync(PresentationProcessor processor)
    {
        await using var document = processor.CreateBlank();
        document.Slides.AddBlank(document.Masters[0].Layouts[0]);
        using var ms = new MemoryStream();
        await processor.SaveAsync(document, ms, cancellationToken: TestContext.Current.CancellationToken);
        return ms.ToArray();
    }

    [Fact]
    public async Task CreateAsync_BuildsTreeAndStartsClean()
    {
        using var processor = new PresentationProcessor();
        var bytes = await PresentationBytesAsync(processor);

        var state = await PptxSessionState.CreateAsync(processor, bytes, "deck.pptx", TestContext.Current.CancellationToken);

        state.FileName.ShouldBe("deck.pptx");
        state.IsDirty.ShouldBeFalse();
        state.CurrentSlide.ShouldBe(1);
        state.PlayboardState.ShouldNotBeNull();
        state.Tree.Label.ShouldBe("deck.pptx");
        state.Document.Slides.Count.ShouldBe(1);

        await state.DisposeAsync();
    }

    [Fact]
    public async Task MarkDirty_SetsDirtyAndRaisesRefreshed()
    {
        using var processor = new PresentationProcessor();
        var state = await PptxSessionState.CreateAsync(processor, await PresentationBytesAsync(processor), "deck.pptx", TestContext.Current.CancellationToken);
        var raised = false;
        state.Refreshed += () => raised = true;

        state.MarkDirty();

        state.IsDirty.ShouldBeTrue();
        raised.ShouldBeTrue();

        await state.DisposeAsync();
    }

    [Fact]
    public async Task RefreshAsync_SerializesReloadsAndClearsDirty()
    {
        using var processor = new PresentationProcessor();
        var state = await PptxSessionState.CreateAsync(processor, await PresentationBytesAsync(processor), "deck.pptx", TestContext.Current.CancellationToken);
        state.MarkDirty();

        await state.RefreshAsync(TestContext.Current.CancellationToken);

        state.IsDirty.ShouldBeFalse();
        state.CurrentBytes.ShouldNotBeEmpty();
        state.FileSizeBytes.ShouldBe(state.CurrentBytes.LongLength);
        state.Document.Slides.Count.ShouldBe(1);

        await state.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        using var processor = new PresentationProcessor();
        var state = await PptxSessionState.CreateAsync(processor, await PresentationBytesAsync(processor), "deck.pptx", TestContext.Current.CancellationToken);

        await state.DisposeAsync();

        await Should.NotThrowAsync(async () => await state.DisposeAsync());
    }
}
