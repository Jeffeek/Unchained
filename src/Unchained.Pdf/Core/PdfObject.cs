using System.Collections.Concurrent;

namespace Unchained.Pdf.Core;

/// <summary>
/// Base class for all PDF object types defined in ISO 32000-1 §7.3.
/// All derived types are immutable. Equality for primitives (<see cref="PdfBoolean"/>,
/// <see cref="PdfInteger"/>, <see cref="PdfReal"/>, <see cref="PdfName"/>) is value-based;
/// for containers (<see cref="PdfDictionary"/>, <see cref="PdfArray"/>, <see cref="PdfStream"/>)
/// it is reference-based to avoid deep-comparison overhead.
/// </summary>
public abstract class PdfObject;

/// <summary>
/// Represents a PDF boolean object (ISO 32000-1 §7.3.2).
/// Instances are singletons — use <see cref="True"/>, <see cref="False"/>,
/// or <see cref="FromBool"/> instead of constructing directly.
/// </summary>
public sealed class PdfBoolean : PdfObject
{
    /// <summary>The singleton <c>true</c> value.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfBoolean True = new(true);

    /// <summary>The singleton <c>false</c> value.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfBoolean False = new(false);

    private PdfBoolean(bool value) => Value = value;

    /// <summary>The underlying boolean value.</summary>
    // ReSharper disable once MemberCanBeInternal
    public bool Value { get; }

    /// <summary>
    /// Returns <see cref="True"/> or <see cref="False"/> without allocating.
    /// </summary>
    public static PdfBoolean FromBool(bool value) => value ? True : False;

    /// <inheritdoc />
    public override string ToString() => Value ? "true" : "false";
}

/// <summary>
/// Represents a PDF integer object (ISO 32000-1 §7.3.3).
/// The value is stored as <see langword="long"/> to cover the full PDF integer range.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class PdfInteger(long value) : PdfObject
{
    /// <summary>The integer value.</summary>
    // ReSharper disable once MemberCanBeInternal
    public long Value { get; } = value;

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Represents a PDF real (floating-point) number object (ISO 32000-1 §7.3.3).
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class PdfReal(double value) : PdfObject
{
    /// <summary>The real value.</summary>
    // ReSharper disable once MemberCanBeInternal
    public double Value { get; } = value;

    /// <inheritdoc />
    public override string ToString() => Value.ToString("G");
}

/// <summary>
/// Represents a PDF string object (ISO 32000-1 §7.3.4) in either literal <c>(...)</c>
/// or hexadecimal <c>&lt;...&gt;</c> encoding.
/// Raw bytes are stored without decoding to preserve round-trip fidelity.
/// Use <see cref="FromLatin1"/> or <see cref="FromUtf16"/> to create strings from .NET strings.
/// </summary>
public sealed class PdfString(ReadOnlyMemory<byte> bytes, bool isHex = false) : PdfObject
{
    /// <summary>
    /// Raw bytes of the string as they appear in the source PDF, without decoding.
    /// For PDF text strings, the encoding is either PDFDocEncoding (Latin-1 subset)
    /// or UTF-16 big-endian (indicated by a 0xFE 0xFF BOM prefix).
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public ReadOnlyMemory<byte> Bytes { get; } = bytes;

    /// <summary>
    /// <see langword="true"/> if the string originated from a <c>&lt;hex&gt;</c>
    /// token; <see langword="false"/> for a <c>(literal)</c> token.
    /// This flag is preserved for round-trip serialization fidelity.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public bool IsHex { get; } = isHex;

    /// <summary>
    /// Creates a PDF literal string from a .NET string using Latin-1 (ISO 8859-1) encoding.
    /// Suitable for ASCII-range metadata fields such as author or title.
    /// </summary>
    public static PdfString FromLatin1(string value) =>
        new(System.Text.Encoding.Latin1.GetBytes(value));

    /// <summary>
    /// Creates a PDF string from a .NET string using UTF-16 big-endian encoding,
    /// which is the standard encoding for Unicode text strings in PDF (§7.9.2).
    /// The caller is responsible for prepending the BOM (0xFE 0xFF) if required.
    /// </summary>
    public static PdfString FromUtf16(string value) =>
        new(System.Text.Encoding.BigEndianUnicode.GetBytes(value));
}

/// <summary>
/// Represents a PDF name object (ISO 32000-1 §7.3.5), such as <c>/Type</c> or <c>/Page</c>.
/// All instances are interned: <see cref="Get"/> always returns the same object for the same
/// string, so equality can be tested by reference. Common names are pre-interned as
/// <see langword="static readonly"/> fields on this class.
/// </summary>
public sealed class PdfName : PdfObject, IEquatable<PdfName>
{
    private static readonly ConcurrentDictionary<string, PdfName> Intern = new();

    private PdfName(string value) => Value = value;

    /// <summary>The name string without the leading <c>/</c> delimiter.</summary>
    // ReSharper disable once MemberCanBeInternal
    public string Value { get; }

    /// <summary>
    /// Returns the interned <see cref="PdfName"/> instance for <paramref name="value"/>.
    /// Thread-safe. The leading <c>/</c> must NOT be included in <paramref name="value"/>.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public static PdfName Get(string value) =>
        Intern.GetOrAdd(value, static v => new PdfName(v));

    // Pre-interned names for the most common PDF dictionary keys.
    /// <summary>The <c>/Type</c> name.</summary>
    public static readonly PdfName Type = Get("Type");
    /// <summary>The <c>/Subtype</c> name.</summary>
    public static readonly PdfName Subtype = Get("Subtype");
    /// <summary>The <c>/Page</c> name.</summary>
    public static readonly PdfName Page = Get("Page");
    /// <summary>The <c>/Pages</c> name.</summary>
    public static readonly PdfName Pages = Get("Pages");
    /// <summary>The <c>/Catalog</c> name.</summary>
    public static readonly PdfName Catalog = Get("Catalog");
    /// <summary>The <c>/Kids</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Kids = Get("Kids");
    /// <summary>The <c>/Count</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Count = Get("Count");
    /// <summary>The <c>/MediaBox</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName MediaBox = Get("MediaBox");
    /// <summary>The <c>/Resources</c> name.</summary>
    public static readonly PdfName Resources = Get("Resources");
    /// <summary>The <c>/Contents</c> name.</summary>
    public static readonly PdfName Contents = Get("Contents");
    /// <summary>The <c>/Length</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Length = Get("Length");
    /// <summary>The <c>/Filter</c> name.</summary>
    public static readonly PdfName Filter = Get("Filter");
    /// <summary>The <c>/Root</c> name.</summary>
    public static readonly PdfName Root = Get("Root");
    /// <summary>The <c>/Info</c> name.</summary>
    public static readonly PdfName Info = Get("Info");
    /// <summary>The <c>/Size</c> name.</summary>
    public static readonly PdfName Size = Get("Size");
    /// <summary>The <c>/Prev</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Prev = Get("Prev");
    /// <summary>The <c>/Font</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Font = Get("Font");
    /// <summary>The <c>/BaseFont</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName BaseFont = Get("BaseFont");
    /// <summary>The <c>/Parent</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Parent = Get("Parent");
    /// <summary>The <c>/Outlines</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Outlines = Get("Outlines");

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="other"/> is the same interned instance.
    /// Because all <see cref="PdfName"/> values are interned, reference equality is sufficient.
    /// </summary>
    public bool Equals(PdfName? other) => ReferenceEquals(this, other);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PdfName n && Equals(n);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <summary>Returns the name in PDF syntax, e.g. <c>/Type</c>.</summary>
    public override string ToString() => $"/{Value}";
}

/// <summary>
/// Represents a PDF array object (ISO 32000-1 §7.3.6).
/// Elements are heterogeneous <see cref="PdfObject"/> instances and are stored in order.
/// </summary>
public sealed class PdfArray(IReadOnlyList<PdfObject> elements) : PdfObject
{
    /// <summary>A shared empty array. Use instead of allocating <c>new PdfArray([])</c>.</summary>
    public static readonly PdfArray Empty = new([]);

    /// <summary>The ordered list of elements in this array.</summary>
    // ReSharper disable once MemberCanBeInternal
    public IReadOnlyList<PdfObject> Elements { get; } = elements;

    /// <summary>The number of elements in the array.</summary>
    // ReSharper disable once MemberCanBeInternal
    public int Count => Elements.Count;

    /// <summary>Returns the element at the given zero-based <paramref name="index"/>.</summary>
    // ReSharper disable once MemberCanBeInternal
    public PdfObject this[int index] => Elements[index];
}

/// <summary>
/// Represents a PDF dictionary object (ISO 32000-1 §7.3.7).
/// Keys are PDF name strings (without the leading <c>/</c>); values are any <see cref="PdfObject"/>.
/// </summary>
public sealed class PdfDictionary(IReadOnlyDictionary<string, PdfObject> entries) : PdfObject
{
    /// <summary>Creates an empty dictionary.</summary>
    public PdfDictionary() : this(new Dictionary<string, PdfObject>()) { }

    /// <summary>
    /// Returns the value associated with <paramref name="name"/>,
    /// or <see langword="null"/> if the key is absent.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public PdfObject? this[string name] => entries.GetValueOrDefault(name);

    /// <summary>
    /// Returns the value associated with the given <see cref="PdfName"/>,
    /// or <see langword="null"/> if the key is absent.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public PdfObject? this[PdfName name] => entries.GetValueOrDefault(name.Value);

    /// <summary>All key-value pairs in this dictionary.</summary>
    // ReSharper disable once MemberCanBeInternal
    public IReadOnlyDictionary<string, PdfObject> Entries => entries;

    /// <summary>
    /// Returns the value for <paramref name="name"/> cast to <typeparamref name="T"/>,
    /// or <see langword="null"/> if the key is absent or the value is a different type.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public T? Get<T>(string name)
        where T : PdfObject => entries.GetValueOrDefault(name) as T;

    /// <summary>
    /// Returns the value for <paramref name="name"/> cast to <typeparamref name="T"/>,
    /// or <see langword="null"/> if the key is absent or the value is a different type.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public T? Get<T>(PdfName name)
        where T : PdfObject => Get<T>(name.Value);

    /// <summary>
    /// Attempts to retrieve the value for <paramref name="name"/> as <typeparamref name="T"/>.
    /// Returns <see langword="true"/> and sets <paramref name="value"/> on success;
    /// returns <see langword="false"/> and sets <paramref name="value"/> to <see langword="null"/> otherwise.
    /// </summary>
    public bool TryGet<T>(string name, out T value)
        where T : PdfObject
    {
        if (entries.GetValueOrDefault(name) is T t)
        {
            value = t;
            return true;
        }

        value = null!;
        return false;
    }

    /// <summary>
    /// Returns the string value of a <see cref="PdfName"/> entry,
    /// or <see langword="null"/> if the key is absent or is not a name.
    /// Equivalent to <c>Get&lt;PdfName&gt;(key)?.Value</c>.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public string? GetName(string key) => Get<PdfName>(key)?.Value;
}

/// <summary>
/// Represents a PDF stream object (ISO 32000-1 §7.3.8): a dictionary followed by
/// a sequence of bytes. The raw bytes may be compressed; callers must apply the
/// appropriate filter (e.g. <c>FlateDecode</c>) to obtain the decoded content.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class PdfStream(PdfDictionary dictionary, ReadOnlyMemory<byte> data) : PdfObject
{
    /// <summary>The stream dictionary, which describes the stream's length, filter, and other attributes.</summary>
    // ReSharper disable once MemberCanBeInternal
    public PdfDictionary Dictionary { get; } = dictionary;

    /// <summary>
    /// Raw (possibly compressed) stream bytes as they appear in the source file.
    /// This is a zero-copy slice into the original source buffer.
    /// Apply the filter chain declared in <c>/Filter</c> to decode.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public ReadOnlyMemory<byte> Data { get; } = data;

    /// <summary>
    /// The declared byte length from the <c>/Length</c> entry in <see cref="Dictionary"/>.
    /// Falls back to <c>Data.Length</c> if the entry is absent or malformed.
    /// </summary>
    public int DeclaredLength =>
        (Dictionary.Get<PdfInteger>(PdfName.Length)?.Value ?? Data.Length) is var l
            ? (int)l
            : Data.Length;
}

/// <summary>
/// Represents the PDF null object (ISO 32000-1 §7.3.9).
/// There is exactly one instance: <see cref="Instance"/>.
/// </summary>
public sealed class PdfNull : PdfObject
{
    /// <summary>The singleton null value.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfNull Instance = new();

    private PdfNull() { }

    /// <inheritdoc />
    public override string ToString() => "null";
}

/// <summary>
/// Represents an indirect object reference in the form <c>N G R</c>
/// (ISO 32000-1 §7.3.10), where <c>N</c> is the object number and <c>G</c>
/// is the generation number. The referenced object is resolved on demand
/// by <see cref="Unchained.Pdf.Document.PdfDocumentCore"/>.
/// </summary>
public sealed class PdfIndirectReference(int objectNumber, int generation) : PdfObject, IEquatable<PdfIndirectReference>
{
    /// <summary>The object number (1-based, unique within the document).</summary>
    // ReSharper disable once MemberCanBeInternal
    public int ObjectNumber { get; } = objectNumber;

    /// <summary>
    /// The generation number. Zero for all objects in non-incrementally-updated PDFs.
    /// Increments each time an object is freed and reused (ISO 32000-1 §7.5.4).
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public int Generation { get; } = generation;

    /// <inheritdoc />
    public bool Equals(PdfIndirectReference? other) =>
        other is not null && ObjectNumber == other.ObjectNumber && Generation == other.Generation;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PdfIndirectReference r && Equals(r);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(ObjectNumber, Generation);

    /// <summary>Returns the reference in PDF syntax, e.g. <c>5 0 R</c>.</summary>
    public override string ToString() => $"{ObjectNumber} {Generation} R";
}

/// <summary>
/// Represents a resolved indirect object in the form <c>N G obj ... endobj</c>
/// (ISO 32000-1 §7.3.10). Produced by <see cref="Unchained.Pdf.Parsing.PdfParser.ReadObject"/>
/// and cached by <see cref="Unchained.Pdf.Document.PdfDocumentCore"/>.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class PdfIndirectObject(int objectNumber, int generation, PdfObject value) : PdfObject
{
    /// <summary>The object number.</summary>
    // ReSharper disable once MemberCanBeInternal
    public int ObjectNumber { get; } = objectNumber;

    /// <summary>The generation number.</summary>
    // ReSharper disable once MemberCanBeInternal
    public int Generation { get; } = generation;

    /// <summary>The actual PDF object wrapped by this indirect object definition.</summary>
    // ReSharper disable once MemberCanBeInternal
    public PdfObject Value { get; } = value;

    /// <summary>Returns an <see cref="PdfIndirectReference"/> pointing to this object.</summary>
    public PdfIndirectReference ToReference() => new(ObjectNumber, Generation);

    /// <inheritdoc />
    public override string ToString() => $"{ObjectNumber} {Generation} obj ... endobj";
}
