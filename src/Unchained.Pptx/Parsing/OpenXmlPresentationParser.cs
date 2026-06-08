using DocumentFormat.OpenXml.Packaging;
using Unchained.Ooxml;
using Unchained.Ooxml.Engine;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Security;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Themes;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Phase 2 OpenXML-SDK-backed presentation reader. Produces the same <see cref="ParsedPresentation"/>
/// typed model as the legacy custom <see cref="PresentationParser"/>, but maps it from the SDK's
/// validated typed DOM (<c>DocumentFormat.OpenXml</c>) instead of hand-rolled XElement walking.
/// </summary>
/// <remarks>
/// This runs in parallel with the custom parser (selected via <c>OpenOptions.UseOpenXmlEngine</c>)
/// and is being grown to full parity. It currently maps: slide size, core document properties,
/// slide structure (id / name / hidden), and per-shape geometry + text. Masters/layouts are
/// represented minimally so the model is internally consistent.
/// </remarks>
internal static class OpenXmlPresentationParser
{
    public static ParsedPresentation Parse(byte[] data, OpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var engine = OoxmlEngine.Open(data, editable: false);
        if (engine.Format != OoxmlFormat.Presentation)
            throw new PptxException($"Expected a presentation package but found {engine.Format}.");

        var doc = (PresentationDocument)engine.Package;
        var presPart = doc.PresentationPart
                       ?? throw new PptxException("The presentation package has no presentation part.");

        var masters = new MasterSlideCollection();
        var slides = new SlideCollection();
        var mediaStore = new MediaStore();
        var commentAuthors = new CommentAuthorCollection();
        var sections = new SectionCollection();

        var slideSize = ReadSlideSize(presPart);
        var properties = ReadProperties(doc);

        // A single minimal master + layout keeps the model self-consistent (every slide needs a
        // Layout whose Master resolves). Full master/layout mapping is a later parity step.
        var master = new MasterSlide { Name = "Office Theme", Theme = new PptxTheme() };
        var fallbackLayout = new SlideLayout
        {
            Name = "Blank",
            LayoutType = Models.Themes.LayoutType.Blank,
            Master = master
        };
        master.Layouts.Add(fallbackLayout);
        masters.Add(master);

        // Slides, in presentation order.
        var slideIndex = 0;
        foreach (var slidePart in EnumerateSlidesInOrder(presPart))
        {
            slideIndex++;
            var slide = ReadSlide(slidePart, fallbackLayout, (uint)(255 + slideIndex));
            slides.AddParsed(slide);
        }

        properties.SlideCount = slides.Count;
        properties.HiddenSlideCount = slides.Count(static s => s.IsHidden);

        return new ParsedPresentation(
            package: null, // the SDK engine owns the real package; not consumed downstream
            slides,
            masters,
            mediaStore,
            properties,
            protection: new ProtectionInfo(),
            slideSize,
            commentAuthors,
            sections);
    }

    // ── Slide size ───────────────────────────────────────────────────────────────

    private static SlideSize ReadSlideSize(PresentationPart presPart)
    {
        var sz = presPart.Presentation?.SlideSize;
        if (sz?.Cx is null || sz.Cy is null)
            return SlideSize.Widescreen;
        return new SlideSize(new Emu(sz.Cx!.Value), new Emu(sz.Cy!.Value));
    }

    // ── Document properties ────────────────────────────────────────────────────────

    private static DocumentProperties ReadProperties(PresentationDocument doc)
    {
        var props = new DocumentProperties();
        var core = doc.PackageProperties;
        props.Title = core.Title;
        props.Subject = core.Subject;
        props.Author = core.Creator;
        props.Keywords = core.Keywords;
        props.Description = core.Description;
        props.LastModifiedBy = core.LastModifiedBy;
        props.Category = core.Category;
        props.ContentStatus = core.ContentStatus;
        if (core.Created is { } created) props.Created = created;
        if (core.Modified is { } modified) props.Modified = modified;
        return props;
    }

    // ── Slides ───────────────────────────────────────────────────────────────────

    private static IEnumerable<SlidePart> EnumerateSlidesInOrder(PresentationPart presPart)
    {
        // Presentation.SlideIdList preserves authoring order; map each r:id to its SlidePart.
        var idList = presPart.Presentation?.SlideIdList;
        if (idList is null)
        {
            foreach (var sp in presPart.SlideParts) yield return sp;
            yield break;
        }

        foreach (var slideId in idList.Elements<P.SlideId>())
        {
            var rId = slideId.RelationshipId?.Value;
            if (rId is null) continue;
            if (presPart.GetPartById(rId) is SlidePart sp)
                yield return sp;
        }
    }

    private static Slide ReadSlide(SlidePart slidePart, SlideLayout layout, uint slideId)
    {
        var slide = new Slide
        {
            Layout = layout,
            SlideId = slideId
        };

        // <p:sld show="0"> means hidden; absence means visible.
        var show = slidePart.Slide?.Show;
        slide.IsHidden = show is not null && !show.Value;

        var tree = slidePart.Slide?.CommonSlideData?.ShapeTree;
        if (tree is not null)
        {
            foreach (var sp in tree.Elements<P.Shape>())
                slide.Shapes.AddParsed(ReadShape(sp));
        }

        return slide;
    }

    // ── Shapes ───────────────────────────────────────────────────────────────────

    private static Shape ReadShape(P.Shape sp)
    {
        var shape = new AutoShape();

        var nv = sp.NonVisualShapeProperties?.NonVisualDrawingProperties;
        if (nv is not null)
        {
            shape.Name = nv.Name?.Value ?? string.Empty;
            shape.AltText = nv.Description?.Value;
        }

        // Geometry from <a:xfrm><a:off/><a:ext/>.
        var xfrm = sp.ShapeProperties?.Transform2D;
        if (xfrm?.Offset is { } off)
        {
            shape.X = new Emu(off.X ?? 0);
            shape.Y = new Emu(off.Y ?? 0);
        }
        if (xfrm?.Extents is { } ext)
        {
            shape.Width = new Emu(ext.Cx ?? 0);
            shape.Height = new Emu(ext.Cy ?? 0);
        }

        // Text body → paragraphs/runs.
        if (sp.TextBody is { } body)
        {
            shape.IsTextBox = true;
            ReadTextBody(body, shape.TextFrame);
        }

        return shape;
    }

    private static void ReadTextBody(P.TextBody body, TextFrame frame)
    {
        foreach (var para in body.Elements<D.Paragraph>())
        {
            var p = frame.Paragraphs.Add();
            foreach (var run in para.Elements<D.Run>())
            {
                var text = run.Text?.Text;
                if (!string.IsNullOrEmpty(text))
                    p.Runs.Add(text);
            }
        }
    }
}
