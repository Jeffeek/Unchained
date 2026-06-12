using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;

namespace Unchained.Pptx.Tests.Helpers;

/// <summary>
///     In-memory PPTX builders — no files required for unit or most integration tests.
/// </summary>
public static class PptxFixtures
{
    /// <summary>Creates a blank <see cref="PresentationDocument" /> (widescreen, no slides).</summary>
    public static PresentationDocument BlankPresentation()
    {
        var processor = new PresentationProcessor();
        return processor.CreateBlank(SlideSize.Widescreen);
    }

    /// <summary>
    ///     Creates a <see cref="PresentationDocument" /> with <paramref name="slideCount" /> blank slides.
    /// </summary>
    public static PresentationDocument WithSlides(int slideCount)
    {
        var processor = new PresentationProcessor();
        var doc = processor.CreateBlank(SlideSize.Widescreen);
        var layout = doc.Masters[0].Layouts[0];
        for (var i = 0; i < slideCount; i++)
            doc.Slides.AddBlank(layout);
        return doc;
    }

    /// <summary>Serializes <paramref name="document" /> to bytes and reloads it.</summary>
    public static async Task<PresentationDocument> RoundTripAsync(PresentationDocument document)
    {
        var processor = new PresentationProcessor();
        var ms = new MemoryStream();
        await processor.SaveAsync(document, ms);
        ms.Position = 0;
        return await processor.LoadAsync(ms);
    }
}
