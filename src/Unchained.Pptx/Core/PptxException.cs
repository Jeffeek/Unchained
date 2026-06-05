namespace Unchained.Pptx.Core;

/// <summary>
/// The exception thrown when an error is encountered while reading, writing, or
/// processing a presentation file.
/// </summary>
public class PptxException : Exception
{
    /// <summary>Initialises a new <see cref="PptxException"/> with the given message.</summary>
    public PptxException(string message) : base(message) { }

    /// <summary>
    /// Initialises a new <see cref="PptxException"/> with the given message and inner exception.
    /// </summary>
    public PptxException(string message, Exception innerException) : base(message, innerException) { }
}
