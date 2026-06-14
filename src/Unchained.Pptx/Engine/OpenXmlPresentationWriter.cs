using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Unchained.Ooxml.Engine;
using Unchained.Pptx.Core;
using Unchained.Pptx.Writing;
using P = DocumentFormat.OpenXml.Presentation;
using SdkPresentationDocument = DocumentFormat.OpenXml.Packaging.PresentationDocument;

namespace Unchained.Pptx.Engine;

/// <summary>
///     Phase 2 (M5b) SDK-backed save. Re-emits the modelled slide content onto the document's held
///     <see cref="OoxmlEngine" /> package and saves through it, so every OPC part the model does not
///     own (chart styles, embeddings, tags, presProps, etc.) passes through unchanged — fixing the
///     part-dropping the custom writer suffers.
/// </summary>
/// <remarks>
///     First slice: slides only. Each model slide's shape tree is regenerated with the existing
///     <see cref="SlideWriter" /> and set as the corresponding SDK slide part's root element (flushed
///     via the part's own Save). Masters, layouts, themes, notes, charts, and media are left as the
///     SDK loaded them — round-trip-preserved — pending later slices. Requires the document to carry
///     a live engine (SDK load path); callers must check <see cref="CanSave" /> first.
/// </remarks>
internal static class OpenXmlPresentationWriter
{
    /// <summary>Whether <paramref name="document" /> can be saved through the SDK engine.</summary>
    public static bool CanSave(PresentationDocument document) => document.Engine is not null;

    /// <summary>
    ///     Re-emits modelled content onto the held SDK package and returns the saved bytes.
    ///     Throws when the document has no attached engine.
    /// </summary>
    public static byte[] Save(PresentationDocument document)
    {
        var engine = document.Engine
                     ?? throw new InvalidOperationException(
                         "SDK-backed save requires a document loaded via the OpenXML engine path."
                     );

        var sdkDoc = (SdkPresentationDocument)engine.Package;
        var presPart = sdkDoc.PresentationPart
                       ?? throw new PptxException("The held package has no presentation part.");

        // SDK slide parts in presentation (SlideIdList) order — the same order as model slides.
        var slideParts = OrderedSlideParts(presPart).ToList();

        var modelSlides = document.Slides;
        var count = Math.Min(slideParts.Count, modelSlides.Count);
        for (var i = 0; i < count; i++)
        {
            var xml = SlideWriter.Write(modelSlides[i]).ToString(SaveOptions.DisableFormatting);
            var part = slideParts[i];
            part.Slide = new P.Slide(xml);
            part.Slide.Save(); // flush the regenerated DOM to the part stream
        }

        return engine.Save();
    }

    private static IEnumerable<SlidePart> OrderedSlideParts(PresentationPart presPart)
    {
        var idList = presPart.Presentation?.SlideIdList;
        if (idList is null)
        {
            foreach (var sp in presPart.SlideParts)
                yield return sp;

            yield break;
        }

        foreach (var rId in idList.Elements<P.SlideId>().Select(static slideId => slideId.RelationshipId?.Value).OfType<string>())
        {
            if (presPart.GetPartById(rId) is SlidePart sp)
                yield return sp;
        }
    }
}
