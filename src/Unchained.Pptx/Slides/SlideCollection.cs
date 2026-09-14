using System.Collections;
using System.Xml.Linq;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Media;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Writing;

namespace Unchained.Pptx.Slides;

/// <summary>
///     An ordered, mutable collection of <see cref="Slide" /> objects in a presentation.
///     Provides named methods for adding, inserting, reordering, and removing slides.
/// </summary>
public sealed class SlideCollection : IReadOnlyList<Slide>
{
    private readonly List<Slide> _slides = [];

    // Maps a source master (from another presentation) to the master imported into this
    // document, so multiple slides cloned from the same source deck reuse one imported master.
    private readonly Dictionary<MasterSlide, MasterSlide> _importedMasters = new(ReferenceEqualityComparer.Instance);
    private uint _nextSlideId = 256; // OOXML minimum slide ID

    /// <summary>
    ///     The owning document's media store, set when this collection is attached to a
    ///     <see cref="Engine.PresentationDocument" />. Cross-presentation clones import referenced
    ///     media here. <see langword="null" /> for a detached collection.
    /// </summary>
    internal MediaStore? OwnerMedia { get; set; }

    /// <summary>
    ///     The owning document's master collection. Cross-presentation clones import foreign
    ///     masters/layouts here. <see langword="null" /> for a detached collection.
    /// </summary>
    internal MasterSlideCollection? OwnerMasters { get; set; }

    // ── IReadOnlyList<Slide> ──────────────────────────────────────────────────

    /// <inheritdoc />
    public int Count => _slides.Count;

    /// <inheritdoc cref="IReadOnlyList{T}.this" />
    public Slide this[int index] => _slides[index];

    /// <inheritdoc />
    public IEnumerator<Slide> GetEnumerator() => _slides.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        _slides.GetEnumerator();

    // ── Mutation ─────────────────────────────────────────────────────────────

    /// <summary>
    ///     Appends a blank slide using the first layout of the first master and returns it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the presentation has no masters or no layouts.
    /// </exception>
    public Slide AddBlank(SlideLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var slide = CreateSlide(layout);
        _slides.Add(slide);
        RenumberSlides();
        return slide;
    }

    /// <summary>
    ///     Appends a shallow clone of the given slide and returns the new slide.
    ///     Shape objects are deep-copied so changes to one slide do not affect the other.
    /// </summary>
    public Slide AddClone(Slide source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var clone = CloneSlide(source);
        _slides.Add(clone);
        RenumberSlides();
        return clone;
    }

    /// <summary>
    ///     Inserts a blank slide at the given zero-based position and returns it.
    /// </summary>
    public Slide InsertBlank(int index, SlideLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var slide = CreateSlide(layout);
        _slides.Insert(index, slide);
        RenumberSlides();
        return slide;
    }

    /// <summary>
    ///     Inserts a clone of the given slide at the given zero-based position and returns it.
    /// </summary>
    public Slide InsertClone(int index, Slide source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var clone = CloneSlide(source);
        _slides.Insert(index, clone);
        RenumberSlides();
        return clone;
    }

    /// <summary>
    ///     Moves the slide at <paramref name="currentIndex" /> to <paramref name="newIndex" />.
    ///     Both indices are zero-based.
    /// </summary>
    public void MoveTo(int currentIndex, int newIndex)
    {
        if (currentIndex == newIndex) return;

        var slide = _slides[currentIndex];
        _slides.RemoveAt(currentIndex);
        _slides.Insert(newIndex, slide);
        RenumberSlides();
    }

    /// <summary>Removes the given slide from the collection.</summary>
    public void Remove(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        if (!_slides.Remove(slide))
            throw new ArgumentException("The slide does not belong to this collection.", nameof(slide));

        RenumberSlides();
    }

    /// <summary>Removes the slide at the given zero-based index.</summary>
    public void RemoveAt(int index)
    {
        _slides.RemoveAt(index);
        RenumberSlides();
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    /// <summary>Adds a slide that was parsed from a file (preserves its existing ID).</summary>
    internal void AddParsed(Slide slide)
    {
        if (slide.SlideId >= _nextSlideId)
            _nextSlideId = slide.SlideId + 1;
        _slides.Add(slide);
        RenumberSlides();
    }

    private Slide CreateSlide(SlideLayout layout)
    {
        var slide = new Slide
        {
            SlideId = _nextSlideId++,
            Layout = layout
        };
        return slide;
    }

    private Slide CloneSlide(Slide source)
    {
        var clone = new Slide
        {
            SlideId = _nextSlideId++,
            Name = source.Name,
            IsHidden = source.IsHidden,
            Layout = ResolveLayout(source.Layout)
        };

        CloneShapesInto(source.Shapes, clone.Shapes);
        CopyBackground(source.Background, clone.Background);
        return clone;
    }

    // Deep-copies shapes from source into target via an XML serialize/re-parse round-trip, then
    // transfers resource references (images, audio, video) that the XML round-trip cannot carry —
    // importing referenced media into this document's store so it survives a save.
    private void CloneShapesInto(ShapeCollection source, ShapeCollection target)
    {
        var writable = source
            .Select(static shape => (Shape: shape, Element: ShapeWriter.Write(shape)))
            .Where(static x => x.Element is not null)
            .ToList();

        // Copy each written element (some shape writers return the shape's own preserved
        // RawElement — e.g. SmartArt — so a fresh copy keeps the clone's XML independent of source).
        var spTree = new XElement(PmlNames.GroupShape);
        foreach (var (_, element) in writable)
            spTree.Add(new XElement(element!));

        var parsedCollection = new ShapeCollection();
        new ShapeParser().ParseTree(spTree, parsedCollection);
        var parsed = parsedCollection.ToList();

        for (var i = 0; i < Math.Min(writable.Count, parsed.Count); i++)
            TransferResources(writable[i].Shape, parsed[i]);

        foreach (var shape in parsed)
            target.AddParsed(shape);
    }

    // Copies media object references from a source shape onto its freshly-parsed clone, recursing
    // into groups. The XML round-trip only preserves the relationship id, not the resolved media
    // object, so without this the clone's image/audio/video would be lost.
    private void TransferResources(Shape source, Shape clone)
    {
        switch (source, clone)
        {
            case (PictureShape sourcePicture, PictureShape clonePicture):
                clonePicture.Image = ImportImage(sourcePicture.Image);
            break;
            case (VideoShape sourceVideo, VideoShape cloneVideo):
                cloneVideo.Video = sourceVideo.Video is null ? null : OwnerMedia?.ImportVideo(sourceVideo.Video) ?? sourceVideo.Video;
                cloneVideo.PosterFrame = ImportImage(sourceVideo.PosterFrame);
            break;
            case (AudioShape sourceAudio, AudioShape cloneAudio):
                cloneAudio.Audio = sourceAudio.Audio is null ? null : OwnerMedia?.ImportAudio(sourceAudio.Audio) ?? sourceAudio.Audio;
            break;
            case (ChartShape sourceChart, ChartShape cloneChart):
                // The chart part data carries the chart verbatim; regenerate the graphic frame
                // (clear RawElement/identity) so the writer assigns a fresh, non-colliding rId.
                cloneChart.Chart = sourceChart.Chart;
                cloneChart.ChartPartData = sourceChart.ChartPartData;
                cloneChart.RelatedParts.Clear();
                foreach (var relatedPart in sourceChart.RelatedParts)
                    cloneChart.RelatedParts.Add(relatedPart);
                cloneChart.RawElement = null;
                cloneChart.RelationshipId = string.Empty;
                cloneChart.PartUri = string.Empty;
            break;
            case (SmartArtShape sourceArt, SmartArtShape cloneArt):
                CopySmartArt(sourceArt, cloneArt);
            break;
            case (GroupShape sourceGroup, GroupShape cloneGroup):
                var count = Math.Min(sourceGroup.Children.Count, cloneGroup.Children.Count);
                for (var i = 0; i < count; i++)
                    TransferResources(sourceGroup.Children[i], cloneGroup.Children[i]);
            break;
        }
    }

    private EmbeddedImage? ImportImage(EmbeddedImage? source) =>
        source is null ? null : OwnerMedia?.ImportImage(source) ?? source;

    // Deep-copies a SmartArt diagram: node tree, the editable data document, and every preserved
    // part's bytes. Relationship IDs and part URIs are cleared so the writer assigns fresh,
    // non-colliding identity (and patches the graphic frame) on save.
    private static void CopySmartArt(SmartArtShape source, SmartArtShape clone)
    {
        clone.Nodes.Clear();
        foreach (var node in source.Nodes)
            clone.Nodes.Add(CopyNode(node));

        clone.DiagramDataDocument = source.DiagramDataDocument is null ? null : new XDocument(source.DiagramDataDocument);
        clone.DataPartData = source.DataPartData;
        clone.LayoutPartData = source.LayoutPartData;
        clone.QuickStylePartData = source.QuickStylePartData;
        clone.ColorsPartData = source.ColorsPartData;
        clone.DrawingPartData = source.DrawingPartData;

        clone.DataRelationshipId = string.Empty;
        clone.LayoutRelationshipId = string.Empty;
        clone.QuickStyleRelationshipId = string.Empty;
        clone.ColorsRelationshipId = string.Empty;
        clone.DrawingRelationshipId = string.Empty;
        clone.DataPartUri = string.Empty;
        clone.LayoutPartUri = string.Empty;
        clone.QuickStylePartUri = string.Empty;
        clone.ColorsPartUri = string.Empty;
        clone.DrawingPartUri = string.Empty;

        return;

        static SmartArtNode CopyNode(SmartArtNode source)
        {
            var copy = new SmartArtNode { ModelId = source.ModelId, Text = source.Text };
            foreach (var child in source.Children)
                copy.Children.Add(CopyNode(child));
            return copy;
        }
    }

    // Copies a slide/master/layout background fill. Solid/gradient/pattern settings are shared by
    // reference (immutable configuration); a picture fill is rebuilt with its image imported into
    // this document's media store so it resolves on save.
    private void CopyBackground(SlideBackground source, SlideBackground target)
    {
        var sourceFill = source.Fill;
        var targetFill = target.Fill;
        targetFill.Type = sourceFill.Type;
        targetFill.Solid = sourceFill.Solid;
        targetFill.Gradient = sourceFill.Gradient;
        targetFill.Pattern = sourceFill.Pattern;
        targetFill.Picture = sourceFill.Picture is null
            ? null
            : new PictureFill { Image = ImportImage(sourceFill.Picture.Image), StretchMode = sourceFill.Picture.StretchMode };
    }

    // Resolves the layout a cloned slide should use. A slide cloned within the same presentation
    // keeps its layout; one cloned from another presentation triggers an import of that layout's
    // master subtree (deduplicated per source master) into this document's masters.
    private SlideLayout ResolveLayout(SlideLayout? sourceLayout)
    {
        if (sourceLayout is null || OwnerMasters is null || sourceLayout.Master == null!)
            return sourceLayout!;

        if (ContainsMaster(sourceLayout.Master))
            return sourceLayout;

        var importedMaster = ImportMaster(sourceLayout.Master);
        var layoutIndex = IndexOfLayout(sourceLayout.Master, sourceLayout);
        if (layoutIndex >= 0 && layoutIndex < importedMaster.Layouts.Count)
            return importedMaster.Layouts[layoutIndex];

        // The source layout was not part of its own master's layout list (detached); clone it
        // directly into the imported master so the slide still resolves to a valid layout.
        var fallback = new SlideLayout
        {
            Name = sourceLayout.Name,
            LayoutType = sourceLayout.LayoutType,
            Master = importedMaster
        };
        CloneShapesInto(sourceLayout.Shapes, fallback.Shapes);
        CopyBackground(sourceLayout.Background, fallback.Background);
        importedMaster.Layouts.Add(fallback);
        return fallback;
    }

    private bool ContainsMaster(MasterSlide master)
    {
        for (var i = 0; i < OwnerMasters!.Count; i++)
        {
            if (ReferenceEquals(OwnerMasters[i], master))
                return true;
        }

        return false;
    }

    private static int IndexOfLayout(MasterSlide master, SlideLayout layout)
    {
        for (var i = 0; i < master.Layouts.Count; i++)
        {
            if (ReferenceEquals(master.Layouts[i], layout))
                return i;
        }

        return -1;
    }

    // Deep-clones a foreign master (its shapes and every layout) into this document's masters,
    // caching the result so repeated clones from the same source deck share one imported master.
    private MasterSlide ImportMaster(MasterSlide source)
    {
        if (_importedMasters.TryGetValue(source, out var cached))
            return cached;

        var clone = new MasterSlide
        {
            Name = source.Name,
            Theme = source.Theme
        };
        CloneShapesInto(source.Shapes, clone.Shapes);
        CopyBackground(source.Background, clone.Background);

        foreach (var sourceLayout in source.Layouts)
        {
            var layoutClone = new SlideLayout
            {
                Name = sourceLayout.Name,
                LayoutType = sourceLayout.LayoutType,
                Master = clone
            };
            CloneShapesInto(sourceLayout.Shapes, layoutClone.Shapes);
            CopyBackground(sourceLayout.Background, layoutClone.Background);
            clone.Layouts.Add(layoutClone);
        }

        OwnerMasters!.Add(clone);
        _importedMasters[source] = clone;
        return clone;
    }

    private void RenumberSlides()
    {
        for (var i = 0; i < _slides.Count; i++)
            _slides[i].SlideNumber = i + 1;
    }
}
