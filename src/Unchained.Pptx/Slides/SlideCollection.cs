using System.Collections;

namespace Unchained.Pptx.Slides;

/// <summary>
///     An ordered, mutable collection of <see cref="Slide" /> objects in a presentation.
///     Provides named methods for adding, inserting, reordering, and removing slides.
/// </summary>
public sealed class SlideCollection : IReadOnlyList<Slide>
{
    private readonly List<Slide> _slides = [];
    private uint _nextSlideId = 256; // OOXML minimum slide ID

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
        // Shallow structural clone — shapes are copied by reference for now.
        // A deeper clone would require a full shape-graph copy visitor.
        var clone = new Slide
        {
            SlideId = _nextSlideId++,
            Name = source.Name,
            IsHidden = source.IsHidden,
            Layout = source.Layout
        };
        foreach (var shape in source.Shapes)
            clone.Shapes.AddParsed(shape);
        return clone;
    }

    private void RenumberSlides()
    {
        for (var i = 0; i < _slides.Count; i++)
            _slides[i].SlideNumber = i + 1;
    }
}
