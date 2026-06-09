using Unchained.Ooxml.Text;

namespace Unchained.Pptx.Slides;

/// <summary>
/// The notes slide attached to a content slide, containing speaker notes and any
/// additional shapes placed on the notes page.
/// </summary>
public sealed class NotesSlide
{
    /// <summary>
    /// The speaker notes text. This is a convenience accessor for the text inside
    /// the notes text placeholder.
    /// </summary>
    public string NotesText
    {
        get => NotesTextFrame?.PlainText ?? string.Empty;
        set
        {
            NotesTextFrame ??= new TextFrame();
            NotesTextFrame.PlainText = value;
        }
    }

    /// <summary>
    /// The full text frame of the notes placeholder, providing access to paragraphs
    /// and character formatting. May be <see langword="null"/> if no notes exist yet.
    /// </summary>
    public TextFrame? NotesTextFrame { get; set; }

    /// <summary>
    /// Raw XML preserved from the source file for round-trip fidelity.
    /// </summary>
    internal System.Xml.Linq.XElement? RawElement { get; set; }
}
