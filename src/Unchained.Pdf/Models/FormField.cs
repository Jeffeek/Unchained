namespace Unchained.Pdf.Models;

/// <summary>
/// Represents a single AcroForm field as read from a PDF document (ISO 32000-1 §12.7).
/// </summary>
/// <param name="Name">
/// Fully-qualified field name (dot-separated partial names from the field hierarchy),
/// e.g. <c>Address.Street</c>.
/// </param>
/// <param name="FieldType">
/// PDF field type: <c>Tx</c> (text), <c>Btn</c> (button/checkbox), <c>Ch</c> (choice),
/// <c>Sig</c> (signature).
/// </param>
/// <param name="Value">
/// Current field value decoded to a string, or <see langword="null"/> when the
/// <c>/V</c> entry is absent.
/// </param>
public sealed record FormField(
    string Name,
    string FieldType,
    string? Value
);
