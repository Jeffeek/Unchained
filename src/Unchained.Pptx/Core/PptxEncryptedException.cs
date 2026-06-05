namespace Unchained.Pptx.Core;

/// <summary>
/// The exception thrown when a presentation is password-protected and the supplied
/// password is missing or incorrect.
/// </summary>
public sealed class PptxEncryptedException : PptxException
{
    /// <summary>
    /// Initialises a new <see cref="PptxEncryptedException"/> with a default message.
    /// </summary>
    public PptxEncryptedException()
        : base("The presentation is password-protected. Supply the correct password via OpenOptions.Password.") { }

    /// <summary>
    /// Initialises a new <see cref="PptxEncryptedException"/> with the given message.
    /// </summary>
    public PptxEncryptedException(string message) : base(message) { }

    /// <summary>
    /// Initialises a new <see cref="PptxEncryptedException"/> with the given message and inner exception.
    /// </summary>
    public PptxEncryptedException(string message, Exception innerException) : base(message, innerException) { }
}
