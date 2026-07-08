using System.Collections.Concurrent;
using System.Text;

namespace Unchained.Pdf.Core;

/// <summary>
///     Base class for all PDF object types defined in ISO 32000-1 §7.3.
///     All derived types are immutable. Equality for primitives (<see cref="PdfBoolean" />,
///     <see cref="PdfInteger" />, <see cref="PdfReal" />, <see cref="PdfName" />) is value-based;
///     for containers (<see cref="PdfDictionary" />, <see cref="PdfArray" />, <see cref="PdfStream" />)
///     it is reference-based to avoid deep-comparison overhead.
/// </summary>
public abstract class PdfObject;

/// <summary>
///     Represents a PDF boolean object (ISO 32000-1 §7.3.2).
///     Instances are singletons — use <see cref="True" />, <see cref="False" />,
///     or <see cref="FromBool" /> instead of constructing directly.
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
    ///     Returns <see cref="True" /> or <see cref="False" /> without allocating.
    /// </summary>
    public static PdfBoolean FromBool(bool value) => value ? True : False;

    /// <inheritdoc />
    public override string ToString() => Value ? "true" : "false";
}

/// <summary>
///     Represents a PDF integer object (ISO 32000-1 §7.3.3).
///     The value is stored as <see langword="long" /> to cover the full PDF integer range.
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
///     Represents a PDF real (floating-point) number object (ISO 32000-1 §7.3.3).
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
///     Represents a PDF string object (ISO 32000-1 §7.3.4) in either literal <c>(...)</c>
///     or hexadecimal <c>&lt;...&gt;</c> encoding.
///     Raw bytes are stored without decoding to preserve round-trip fidelity.
///     Use <see cref="FromLatin1" /> or <see cref="FromUtf16" /> to create strings from .NET strings.
/// </summary>
public sealed class PdfString(ReadOnlyMemory<byte> bytes, bool isHex = false) : PdfObject
{
    /// <summary>
    ///     Raw bytes of the string as they appear in the source PDF, without decoding.
    ///     For PDF text strings, the encoding is either PDFDocEncoding (Latin-1 subset)
    ///     or UTF-16 big-endian (indicated by a 0xFE 0xFF BOM prefix).
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public ReadOnlyMemory<byte> Bytes { get; } = bytes;

    /// <summary>
    ///     <see langword="true" /> if the string originated from a <c>&lt;hex&gt;</c>
    ///     token; <see langword="false" /> for a <c>(literal)</c> token.
    ///     This flag is preserved for round-trip serialization fidelity.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public bool IsHex { get; } = isHex;

    /// <summary>
    ///     Creates a PDF literal string from a .NET string using Latin-1 (ISO 8859-1) encoding.
    ///     Suitable for ASCII-range metadata fields such as author or title.
    /// </summary>
    public static PdfString FromLatin1(string value) =>
        new(Encoding.Latin1.GetBytes(value));

    /// <summary>
    ///     Creates a PDF string from a .NET string using UTF-16 big-endian encoding,
    ///     which is the standard encoding for Unicode text strings in PDF (§7.9.2).
    ///     The caller is responsible for prepending the BOM (0xFE 0xFF) if required.
    /// </summary>
    public static PdfString FromUtf16(string value) =>
        new(Encoding.BigEndianUnicode.GetBytes(value));

    /// <summary>
    ///     When <see cref="IsHex" /> is <see langword="true" />, decodes the raw hex-digit
    ///     bytes (e.g. <c>{'3','0','3','1'}</c> for <c>&lt;3031&gt;</c>) into the actual
    ///     binary bytes (<c>{0x30, 0x31}</c>). Returns <see cref="Bytes" /> unchanged when
    ///     <see cref="IsHex" /> is <see langword="false" /> (literal string already binary).
    /// </summary>
    internal ReadOnlyMemory<byte> GetBinaryBytes()
    {
        if (!IsHex)
            return Bytes;

        var span = Bytes.Span;
        var result = new byte[(span.Length + 1) / 2];
        var j = 0;
        var hi = -1;
        foreach (var c in span)
        {
            if (c is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
                continue;

            var n = c switch
            {
                >= (byte)'0' and <= (byte)'9' => c - '0',
                >= (byte)'a' and <= (byte)'f' => c - 'a' + 10,
                >= (byte)'A' and <= (byte)'F' => c - 'A' + 10,
                _ => -1
            };
            if (n < 0)
                continue;

            if (hi < 0)
                hi = n;
            else
            {
                result[j++] = (byte)((hi << 4) | n);
                hi = -1;
            }
        }

        if (hi >= 0) result[j++] = (byte)(hi << 4);
        return result.AsMemory(0, j);
    }
}

/// <summary>
///     Represents a PDF name object (ISO 32000-1 §7.3.5), such as <c>/Type</c> or <c>/Page</c>.
///     All instances are interned: <see cref="Get" /> always returns the same object for the same
///     string, so equality can be tested by reference. Common names are pre-interned as
///     <see langword="static readonly" /> fields on this class.
/// </summary>
public sealed class PdfName : PdfObject, IEquatable<PdfName>
{
    private static readonly ConcurrentDictionary<string, PdfName> Intern = new();

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
    /// <summary>The <c>/Annots</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Annots = Get("Annots");
    /// <summary>The <c>/Rect</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Rect = Get("Rect");
    /// <summary>The <c>/AcroForm</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName AcroForm = Get("AcroForm");
    /// <summary>The <c>/Fields</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Fields = Get("Fields");
    /// <summary>The <c>/First</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName First = Get("First");
    /// <summary>The <c>/Last</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Last = Get("Last");
    /// <summary>The <c>/Next</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Next = Get("Next");
    /// <summary>The <c>/Dest</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Dest = Get("Dest");
    /// <summary>The <c>/Title</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Title = Get("Title");
    /// <summary>The <c>/PageLayout</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName PageLayout = Get("PageLayout");
    /// <summary>The <c>/PageMode</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName PageMode = Get("PageMode");
    /// <summary>The <c>/OpenAction</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName OpenAction = Get("OpenAction");
    /// <summary>The <c>/ViewerPreferences</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName ViewerPreferences = Get("ViewerPreferences");
    /// <summary>The <c>/Metadata</c> name (XMP metadata stream).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Metadata = Get("Metadata");
    /// <summary>The <c>/Names</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Names = Get("Names");
    /// <summary>The <c>/Dests</c> name (named destinations).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Dests = Get("Dests");

    // ── Tagged PDF / PDF/UA names ─────────────────────────────────────────────

    /// <summary>The <c>/MarkInfo</c> name (marked-content information dictionary).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName MarkInfo = Get("MarkInfo");

    /// <summary>The <c>/Marked</c> name (flag inside <c>/MarkInfo</c>).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Marked = Get("Marked");

    /// <summary>The <c>/StructTreeRoot</c> name (root of the logical structure tree).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName StructTreeRoot = Get("StructTreeRoot");

    /// <summary>The <c>/StructElem</c> name (structure element type).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName StructElem = Get("StructElem");

    /// <summary>The <c>/MCR</c> name (marked-content reference type).</summary>
    // ReSharper disable once MemberCanBeInternal
    // ReSharper disable once InconsistentNaming
    public static readonly PdfName MCR = Get("MCR");

    /// <summary>The <c>/MCID</c> name (marked-content identifier integer).</summary>
    // ReSharper disable once MemberCanBeInternal
    // ReSharper disable once InconsistentNaming
    public static readonly PdfName MCID = Get("MCID");

    /// <summary>The <c>/ParentTree</c> name (number tree mapping MCIDs to struct elements).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName ParentTree = Get("ParentTree");

    /// <summary>The <c>/ParentTreeNextKey</c> name.</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName ParentTreeNextKey = Get("ParentTreeNextKey");

    /// <summary>The <c>/RoleMap</c> name (maps non-standard structure types to standard ones).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName RoleMap = Get("RoleMap");

    /// <summary>The <c>/Lang</c> name (BCP 47 language tag).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Lang = Get("Lang");

    /// <summary>The <c>/Alt</c> name (alternative text for figures).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Alt = Get("Alt");

    /// <summary>The <c>/ActualText</c> name (actual Unicode text for content).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName ActualText = Get("ActualText");

    /// <summary>The <c>/Pg</c> name (page reference inside a marked-content reference).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Pg = Get("Pg");

    /// <summary>The <c>/K</c> name (kids / content items inside a structure element).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName K = Get("K");

    /// <summary>The <c>/S</c> name (structure type inside a structure element).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName S = Get("S");

    /// <summary>The <c>/P</c> name (parent reference inside a structure element).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName P = Get("P");

    /// <summary>The <c>/Nums</c> name (number array inside a number tree node).</summary>
    // ReSharper disable once MemberCanBeInternal
    public static readonly PdfName Nums = Get("Nums");

    // ── Actions & destinations ────────────────────────────────────────────────

    /// <summary>The <c>/Action</c> name (action dictionary type).</summary>
    public static readonly PdfName Action = Get("Action");

    /// <summary>The <c>/GoTo</c> name (go-to action subtype — jump to a destination in the same document).</summary>
    public static readonly PdfName GoTo = Get("GoTo");

    /// <summary>The <c>/URI</c> name (URI action subtype — resolve a uniform resource identifier).</summary>
    public static readonly PdfName URI = Get("URI");

    /// <summary>The <c>/Named</c> name (named action subtype — a predefined viewer action such as NextPage).</summary>
    public static readonly PdfName Named = Get("Named");

    /// <summary>The <c>/XYZ</c> name (destination type — explicit top-left position plus zoom factor).</summary>
    public static readonly PdfName XYZ = Get("XYZ");

    // ── Shadings & functions ──────────────────────────────────────────────────

    /// <summary>The <c>/BitsPerCoordinate</c> name (mesh shading — bits used per vertex coordinate value).</summary>
    public static readonly PdfName BitsPerCoordinate = Get("BitsPerCoordinate");

    /// <summary>The <c>/BitsPerComponent</c> name (bits per colour component in an image or shading sample).</summary>
    public static readonly PdfName BitsPerComponent = Get("BitsPerComponent");

    /// <summary>The <c>/BitsPerFlag</c> name (mesh shading — bits used per vertex edge flag).</summary>
    public static readonly PdfName BitsPerFlag = Get("BitsPerFlag");

    /// <summary>The <c>/VerticesPerRow</c> name (lattice-form Gouraud-shaded triangle mesh row width).</summary>
    public static readonly PdfName VerticesPerRow = Get("VerticesPerRow");

    /// <summary>
    ///     The <c>/FunctionType</c> name (PDF function dictionary type: 0 sampled, 2 exponential, 3 stitching, 4
    ///     PostScript).
    /// </summary>
    public static readonly PdfName FunctionType = Get("FunctionType");

    // ── Encryption ────────────────────────────────────────────────────────────

    /// <summary>The <c>/Standard</c> name (the standard password-based security handler).</summary>
    public static readonly PdfName Standard = Get("Standard");

    /// <summary>The <c>/StdCF</c> name (the standard crypt filter entry in <c>/CF</c>).</summary>
    public static readonly PdfName StdCF = Get("StdCF");

    /// <summary>The <c>/AESV3</c> name (crypt filter method — AES-256 in CBC mode).</summary>
    public static readonly PdfName AESV3 = Get("AESV3");

    /// <summary>The <c>/DocOpen</c> name (crypt filter event — decrypt when the document is opened).</summary>
    public static readonly PdfName DocOpen = Get("DocOpen");

    /// <summary>The <c>/Encrypt</c> name (the trailer entry referencing the encryption dictionary).</summary>
    public static readonly PdfName Encrypt = Get("Encrypt");

    /// <summary>The <c>/ID</c> name (the file identifier array in the trailer).</summary>
    public static readonly PdfName ID = Get("ID");

    // ── Fonts ─────────────────────────────────────────────────────────────────

    /// <summary>The <c>/Type1</c> name (Type 1 font subtype).</summary>
    public static readonly PdfName Type1 = Get("Type1");

    /// <summary>The <c>/C</c> name (a colour array, e.g. an annotation's border/background colour).</summary>
    public static readonly PdfName C = Get("C");

    /// <summary>The <c>/ColorSpace</c> name (colour space resource or entry).</summary>
    public static readonly PdfName ColorSpace = Get("ColorSpace");

    /// <summary>The <c>/XObject</c> name (external object — form or image — resource category).</summary>
    public static readonly PdfName XObject = Get("XObject");

    /// <summary>The <c>/Alternate</c> name (alternate colour space for an ICCBased colour space).</summary>
    public static readonly PdfName Alternate = Get("Alternate");

    /// <summary>The <c>/N</c> name (number of colour components in an ICCBased stream, or the normal-appearance entry).</summary>
    public static readonly PdfName N = Get("N");

    /// <summary>The <c>/Gamma</c> name (gamma value for a CalGray or CalRGB colour space).</summary>
    public static readonly PdfName Gamma = Get("Gamma");

    /// <summary>The <c>/Matrix</c> name (transformation matrix for a CalRGB/Lab colour space, form, or pattern).</summary>
    public static readonly PdfName Matrix = Get("Matrix");

    /// <summary>The <c>/DescendantFonts</c> name (the CIDFont array of a Type 0 composite font).</summary>
    public static readonly PdfName DescendantFonts = Get("DescendantFonts");

    /// <summary>The <c>/FontDescriptor</c> name (font metrics and embedded-program descriptor dictionary).</summary>
    public static readonly PdfName FontDescriptor = Get("FontDescriptor");

    /// <summary>The <c>/FontFile2</c> name (embedded TrueType font program stream).</summary>
    public static readonly PdfName FontFile2 = Get("FontFile2");

    /// <summary>The <c>/FontFile3</c> name (embedded CFF / OpenType font program stream).</summary>
    public static readonly PdfName FontFile3 = Get("FontFile3");

    /// <summary>The <c>/FontFile</c> name (embedded Type 1 font program stream).</summary>
    public static readonly PdfName FontFile = Get("FontFile");

    /// <summary>The <c>/DW</c> name (default glyph width for a CIDFont).</summary>
    public static readonly PdfName DW = Get("DW");

    /// <summary>The <c>/ToUnicode</c> name (CMap stream mapping character codes to Unicode).</summary>
    public static readonly PdfName ToUnicode = Get("ToUnicode");

    /// <summary>The <c>/FontMatrix</c> name (glyph-space-to-text-space matrix for a Type 3 font).</summary>
    public static readonly PdfName FontMatrix = Get("FontMatrix");

    /// <summary>The <c>/Encoding</c> name (font character encoding, by name or differences dictionary).</summary>
    public static readonly PdfName Encoding = Get("Encoding");

    /// <summary>The <c>/Differences</c> name (the code-to-glyph-name override array in an encoding dictionary).</summary>
    public static readonly PdfName Differences = Get("Differences");

    /// <summary>The <c>/CharProcs</c> name (glyph content-stream procedures for a Type 3 font).</summary>
    public static readonly PdfName CharProcs = Get("CharProcs");

    /// <summary>The <c>/Widths</c> name (the per-glyph advance-width array of a simple font).</summary>
    public static readonly PdfName Widths = Get("Widths");

    /// <summary>The <c>/FirstChar</c> name (the character code of the first entry in <c>/Widths</c>).</summary>
    public static readonly PdfName FirstChar = Get("FirstChar");

    // ── Images & XObjects ─────────────────────────────────────────────────────

    /// <summary>The <c>/Width</c> name (image width in samples).</summary>
    public static readonly PdfName Width = Get("Width");

    /// <summary>The <c>/Height</c> name (image height in samples).</summary>
    public static readonly PdfName Height = Get("Height");

    /// <summary>The <c>/SMask</c> name (soft-mask image or transparency group giving per-pixel alpha).</summary>
    public static readonly PdfName SMask = Get("SMask");

    /// <summary>The <c>/BBox</c> name (bounding box of a form XObject, pattern, or annotation appearance).</summary>
    public static readonly PdfName BBox = Get("BBox");

    /// <summary>The <c>/Annot</c> name (annotation object type).</summary>
    public static readonly PdfName Annot = Get("Annot");

    /// <summary>The <c>/Collection</c> name (the catalog entry that marks a PDF portfolio / collection).</summary>
    public static readonly PdfName Collection = Get("Collection");

    /// <summary>The <c>/DecodeParms</c> name (per-filter decode parameters for a stream).</summary>
    public static readonly PdfName DecodeParms = Get("DecodeParms");

    /// <summary>The <c>/JBIG2Globals</c> name (the shared globals stream referenced by a JBIG2-encoded image).</summary>
    public static readonly PdfName JBIG2Globals = Get("JBIG2Globals");

    /// <summary>The <c>/XML</c> name (metadata stream subtype — XMP packet).</summary>
    public static readonly PdfName XML = Get("XML");

    // ── Document-level & viewer preferences ───────────────────────────────────

    /// <summary>The <c>/R2L</c> name (viewer preference — right-to-left reading/page order).</summary>
    public static readonly PdfName R2L = Get("R2L");

    /// <summary>The <c>/Document</c> name (top-level structure type, or output-intent context).</summary>
    public static readonly PdfName Document = Get("Document");

    /// <summary>The <c>/OutputIntent</c> name (output-intent dictionary type for PDF/X, PDF/A, etc.).</summary>
    public static readonly PdfName OutputIntent = Get("OutputIntent");

    /// <summary>The <c>/GTS_PDFX</c> name (output-intent subtype identifying a PDF/X conformance intent).</summary>
    // ReSharper disable once InconsistentNaming
    public static readonly PdfName GTS_PDFX = Get("GTS_PDFX");

    /// <summary>The <c>/DisplayDocTitle</c> name (viewer preference — show the document title in the window title bar).</summary>
    public static readonly PdfName DisplayDocTitle = Get("DisplayDocTitle");

    /// <summary>The <c>/TU</c> name (the user-facing alternate name / tooltip of a form field).</summary>
    public static readonly PdfName TU = Get("TU");

    /// <summary>The <c>/AA</c> name (additional-actions dictionary for a page, field, or annotation).</summary>
    public static readonly PdfName AA = Get("AA");

    // ── Signatures ────────────────────────────────────────────────────────────

    /// <summary>The <c>/Sig</c> name (signature field / value dictionary type).</summary>
    public static readonly PdfName Sig = Get("Sig");

    /// <summary>The <c>/Adobe.PPKLite</c> name (the signature handler / filter for PKCS#7 signatures).</summary>
    // ReSharper disable once InconsistentNaming
    public static readonly PdfName AdobePPKLite = Get("Adobe.PPKLite");

    /// <summary>The <c>/adbe.pkcs7.detached</c> name (signature sub-filter — a detached PKCS#7 / CMS blob).</summary>
    public static readonly PdfName AdbePkcs7Detached = Get("adbe.pkcs7.detached");

    /// <summary>The <c>/Widget</c> name (widget annotation subtype — the on-page appearance of a form field).</summary>
    public static readonly PdfName Widget = Get("Widget");

    /// <summary>The <c>/Fit</c> name (destination type — fit the whole page in the window).</summary>
    public static readonly PdfName Fit = Get("Fit");

    /// <summary>The <c>/FlateDecode</c> name (the zlib/deflate stream filter).</summary>
    public static readonly PdfName FlateDecode = Get("FlateDecode");

    // ── Embedded files ────────────────────────────────────────────────────────

    /// <summary>The <c>/EmbeddedFiles</c> name (the name tree of embedded file specifications in <c>/Names</c>).</summary>
    public static readonly PdfName EmbeddedFiles = Get("EmbeddedFiles");

    /// <summary>The <c>/UF</c> name (the Unicode file name of a file specification).</summary>
    public static readonly PdfName UF = Get("UF");

    /// <summary>The <c>/F</c> name (file specification name / annotation flags, depending on context).</summary>
    public static readonly PdfName F = Get("F");

    /// <summary>The <c>/Desc</c> name (textual description of an embedded file).</summary>
    public static readonly PdfName Desc = Get("Desc");

    /// <summary>The <c>/AP</c> name (annotation appearance-streams dictionary).</summary>
    public static readonly PdfName AP = Get("AP");

    /// <summary>The <c>/T</c> name (the partial field name of a form field).</summary>
    public static readonly PdfName T = Get("T");

    /// <summary>The <c>/EF</c> name (the embedded-file-streams dictionary of a file specification).</summary>
    public static readonly PdfName EF = Get("EF");

    /// <summary>The <c>/EmbeddedFile</c> name (embedded file stream type).</summary>
    public static readonly PdfName EmbeddedFile = Get("EmbeddedFile");

    /// <summary>The <c>/ModDate</c> name (modification date, e.g. in <c>/Info</c> or an embedded file's params).</summary>
    public static readonly PdfName ModDate = Get("ModDate");

    /// <summary>The <c>/Filespec</c> name (file specification object type).</summary>
    public static readonly PdfName Filespec = Get("Filespec");

    // ── Optional content (layers) ─────────────────────────────────────────────

    /// <summary>The <c>/OCProperties</c> name (the catalog's optional-content (layers) configuration).</summary>
    public static readonly PdfName OCProperties = Get("OCProperties");

    /// <summary>The <c>/D</c> name (the default optional-content configuration, or a destination entry).</summary>
    public static readonly PdfName D = Get("D");

    /// <summary>The <c>/OFF</c> name (the array of optional-content groups that start switched off).</summary>
    public static readonly PdfName OFF = Get("OFF");

    /// <summary>The <c>/PageLabels</c> name (the number tree defining page label ranges).</summary>
    public static readonly PdfName PageLabels = Get("PageLabels");

    /// <summary>The <c>/Rotate</c> name (page rotation in degrees, a multiple of 90).</summary>
    public static readonly PdfName Rotate = Get("Rotate");

    // ── Shadings, patterns & graphics state ───────────────────────────────────

    /// <summary>The <c>/Shading</c> name (shading dictionary, or the shading resource category).</summary>
    public static readonly PdfName Shading = Get("Shading");

    /// <summary>The <c>/Pattern</c> name (pattern colour space, or the pattern resource category).</summary>
    public static readonly PdfName Pattern = Get("Pattern");

    /// <summary>The <c>/PatternType</c> name (1 = tiling pattern, 2 = shading pattern).</summary>
    public static readonly PdfName PatternType = Get("PatternType");

    /// <summary>The <c>/PaintType</c> name (tiling pattern paint type: 1 = coloured, 2 = uncoloured).</summary>
    public static readonly PdfName PaintType = Get("PaintType");

    /// <summary>The <c>/ExtGState</c> name (extended graphics-state dictionary or resource category).</summary>
    public static readonly PdfName ExtGState = Get("ExtGState");

    /// <summary>The <c>/G</c> name (the transparency group form XObject of a soft mask).</summary>
    public static readonly PdfName G = Get("G");

    /// <summary>The <c>/BM</c> name (the current blend mode in the graphics state).</summary>
    public static readonly PdfName BM = Get("BM");

    /// <summary>The <c>/A</c> name (the action of an annotation, or the destination array of an outline item).</summary>
    public static readonly PdfName A = Get("A");

    /// <summary>The <c>/ShadingType</c> name (1 function-based, 2 axial, 3 radial, 4–7 mesh).</summary>
    public static readonly PdfName ShadingType = Get("ShadingType");

    /// <summary>The <c>/HideToolbar</c> name (viewer preference — hide the viewer's toolbars).</summary>
    public static readonly PdfName HideToolbar = Get("HideToolbar");

    /// <summary>The <c>/HideMenubar</c> name (viewer preference — hide the viewer's menu bar).</summary>
    public static readonly PdfName HideMenubar = Get("HideMenubar");

    /// <summary>The <c>/HideWindowUI</c> name (viewer preference — hide UI elements inside the document window).</summary>
    public static readonly PdfName HideWindowUI = Get("HideWindowUI");

    /// <summary>The <c>/FitWindow</c> name (viewer preference — resize the window to fit the first page).</summary>
    public static readonly PdfName FitWindow = Get("FitWindow");

    /// <summary>The <c>/CenterWindow</c> name (viewer preference — centre the document window on screen).</summary>
    public static readonly PdfName CenterWindow = Get("CenterWindow");

    /// <summary>The <c>/OCGs</c> name (the array of all optional-content groups in <c>/OCProperties</c>).</summary>
    public static readonly PdfName OCGs = Get("OCGs");

    /// <summary>The <c>/Name</c> (an optional-content group's display name, or a generic name entry).</summary>
    public static readonly PdfName Name = Get("Name");

    /// <summary>The <c>/V</c> name (the value of a form field, or the version entry in some dictionaries).</summary>
    public static readonly PdfName V = Get("V");

    public static readonly PdfName OutputConditionIdentifier = Get("OutputConditionIdentifier");
    public static readonly PdfName RegistryName = Get("RegistryName");
    public static readonly PdfName SubFilter = Get("SubFilter");
    public static readonly PdfName ByteRange = Get("ByteRange");
    public static readonly PdfName M = Get("M");
    public static readonly PdfName Reason = Get("Reason");
    public static readonly PdfName Location = Get("Location");
    public static readonly PdfName ContactInfo = Get("ContactInfo");
    public static readonly PdfName FT = Get("FT");

    /// <summary>The <c>/O</c> name (encryption dictionary — owner password hash).</summary>
    public static readonly PdfName O = Get("O");

    /// <summary>The <c>/U</c> name (encryption dictionary — user password hash).</summary>
    public static readonly PdfName U = Get("U");

    /// <summary>The <c>/OE</c> name (encryption dictionary — owner encryption algorithm).</summary>
    public static readonly PdfName OE = Get("OE");

    /// <summary>The <c>/UE</c> name (encryption dictionary — user encryption algorithm).</summary>
    public static readonly PdfName UE = Get("UE");

    /// <summary>The <c>/AS</c> name (annotation appearance state entry).</summary>
    public static readonly PdfName AS = Get("AS");

    /// <summary>The <c>/Length1</c> name (embedded font program length for Type 1 fonts).</summary>
    public static readonly PdfName Length1 = Get("Length1");

    /// <summary>The <c>/FontName</c> name (the font's PostScript name in a font descriptor).</summary>
    public static readonly PdfName FontName = Get("FontName");

    /// <summary>The <c>/Flags</c> name (flag bits for a font descriptor or structure element).</summary>
    public static readonly PdfName Flags = Get("Flags");

    /// <summary>The <c>/FontBBox</c> name (bounding box of the font's glyph space).</summary>
    public static readonly PdfName FontBBox = Get("FontBBox");

    /// <summary>The <c>/ItalicAngle</c> name (italic angle of the font in degrees).</summary>
    public static readonly PdfName ItalicAngle = Get("ItalicAngle");

    /// <summary>The <c>/Ascent</c> name (ascent of the font in font units).</summary>
    public static readonly PdfName Ascent = Get("Ascent");

    /// <summary>The <c>/Descent</c> name (descent of the font in font units).</summary>
    public static readonly PdfName Descent = Get("Descent");

    /// <summary>The <c>/CapHeight</c> name (cap height of the font in font units).</summary>
    public static readonly PdfName CapHeight = Get("CapHeight");

    /// <summary>The <c>/StemV</c> name (stem thickness of the font).</summary>
    public static readonly PdfName StemV = Get("StemV");

    /// <summary>The <c>/Decode</c> name (decode parameters array for an image or stream).</summary>
    public static readonly PdfName Decode = Get("Decode");

    /// <summary>The <c>/CIDToGIDMap</c> name (CID-to-Glyph-ID mapping for a CID font).</summary>
    // ReSharper disable once InconsistentNaming
    public static readonly PdfName CIDToGIDMap = Get("CIDToGIDMap");

    /// <summary>The <c>/R</c> name (encryption revision in the encryption dictionary).</summary>
    public static readonly PdfName R = Get("R");

    private PdfName(string value) => Value = value;

    /// <summary>The name string without the leading <c>/</c> delimiter.</summary>
    // ReSharper disable once MemberCanBeInternal
    public string Value { get; }

    /// <summary>
    ///     Returns <see langword="true" /> if <paramref name="other" /> is the same interned instance.
    ///     Because all <see cref="PdfName" /> values are interned, reference equality is sufficient.
    /// </summary>
    public bool Equals(PdfName? other) => ReferenceEquals(this, other);

    /// <summary>
    ///     Returns the interned <see cref="PdfName" /> instance for <paramref name="value" />.
    ///     Thread-safe. The leading <c>/</c> must NOT be included in <paramref name="value" />.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public static PdfName Get(string value) =>
        Intern.GetOrAdd(value, static v => new PdfName(v));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PdfName n && Equals(n);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <summary>Returns the name in PDF syntax, e.g. <c>/Type</c>.</summary>
    public override string ToString() => $"/{Value}";
}

/// <summary>
///     Represents a PDF array object (ISO 32000-1 §7.3.6).
///     Elements are heterogeneous <see cref="PdfObject" /> instances and are stored in order.
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

    /// <summary>Returns the element at the given zero-based <paramref name="index" />.</summary>
    // ReSharper disable once MemberCanBeInternal
    public PdfObject this[int index] => Elements[index];
}

/// <summary>
///     Represents a PDF dictionary object (ISO 32000-1 §7.3.7).
///     Keys are PDF name strings (without the leading <c>/</c>); values are any <see cref="PdfObject" />.
/// </summary>
public sealed class PdfDictionary(IReadOnlyDictionary<string, PdfObject> entries) : PdfObject
{
    /// <summary>Creates an empty dictionary.</summary>
    public PdfDictionary() : this(new Dictionary<string, PdfObject>()) { }

    /// <summary>
    ///     Returns the value associated with <paramref name="name" />,
    ///     or <see langword="null" /> if the key is absent.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public PdfObject? this[string name] => entries.GetValueOrDefault(name);

    /// <summary>
    ///     Returns the value associated with the given <see cref="PdfName" />,
    ///     or <see langword="null" /> if the key is absent.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public PdfObject? this[PdfName name] => entries.GetValueOrDefault(name.Value);

    /// <summary>All key-value pairs in this dictionary.</summary>
    // ReSharper disable once MemberCanBeInternal
    public IReadOnlyDictionary<string, PdfObject> Entries => entries;

    /// <summary>
    ///     Returns the value for <paramref name="name" /> cast to <typeparamref name="T" />,
    ///     or <see langword="null" /> if the key is absent or the value is a different type.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public T? Get<T>(string name)
        where T : PdfObject => entries.GetValueOrDefault(name) as T;

    /// <summary>
    ///     Returns the value for <paramref name="name" /> cast to <typeparamref name="T" />,
    ///     or <see langword="null" /> if the key is absent or the value is a different type.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public T? Get<T>(PdfName name)
        where T : PdfObject => Get<T>(name.Value);

    /// <summary>
    ///     Attempts to retrieve the value for <paramref name="name" /> as <typeparamref name="T" />.
    ///     Returns <see langword="true" /> and sets <paramref name="value" /> on success;
    ///     returns <see langword="false" /> and sets <paramref name="value" /> to <see langword="null" /> otherwise.
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
    ///     Returns the string value of a <see cref="PdfName" /> entry,
    ///     or <see langword="null" /> if the key is absent or is not a name.
    ///     Equivalent to <c>Get&lt;PdfName&gt;(key)?.Value</c>.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public string? GetName(string key) => Get<PdfName>(key)?.Value;
}

/// <summary>
///     Represents a PDF stream object (ISO 32000-1 §7.3.8): a dictionary followed by
///     a sequence of bytes. The raw bytes may be compressed; callers must apply the
///     appropriate filter (e.g. <c>FlateDecode</c>) to obtain the decoded content.
/// </summary>
// ReSharper disable once MemberCanBeInternal
public sealed class PdfStream(PdfDictionary dictionary, ReadOnlyMemory<byte> data) : PdfObject
{
    /// <summary>The stream dictionary, which describes the stream's length, filter, and other attributes.</summary>
    // ReSharper disable once MemberCanBeInternal
    public PdfDictionary Dictionary { get; } = dictionary;

    /// <summary>
    ///     Raw (possibly compressed) stream bytes as they appear in the source file.
    ///     This is a zero-copy slice into the original source buffer.
    ///     Apply the filter chain declared in <c>/Filter</c> to decode.
    /// </summary>
    // ReSharper disable once MemberCanBeInternal
    public ReadOnlyMemory<byte> Data { get; } = data;

    /// <summary>
    ///     The declared byte length from the <c>/Length</c> entry in <see cref="Dictionary" />.
    ///     Falls back to <c>Data.Length</c> if the entry is absent or malformed.
    /// </summary>
    public int DeclaredLength =>
        (Dictionary.Get<PdfInteger>(PdfName.Length)?.Value ?? Data.Length) is var l
            ? (int)l
            : Data.Length;
}

/// <summary>
///     Represents the PDF null object (ISO 32000-1 §7.3.9).
///     There is exactly one instance: <see cref="Instance" />.
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
///     Represents an indirect object reference in the form <c>N G R</c>
///     (ISO 32000-1 §7.3.10), where <c>N</c> is the object number and <c>G</c>
///     is the generation number. The referenced object is resolved on demand
///     by <see cref="Unchained.Pdf.Document.PdfDocumentCore" />.
/// </summary>
public sealed class PdfIndirectReference(int objectNumber, int generation) : PdfObject, IEquatable<PdfIndirectReference>
{
    /// <summary>The object number (1-based, unique within the document).</summary>
    // ReSharper disable once MemberCanBeInternal
    public int ObjectNumber { get; } = objectNumber;

    /// <summary>
    ///     The generation number. Zero for all objects in non-incrementally-updated PDFs.
    ///     Increments each time an object is freed and reused (ISO 32000-1 §7.5.4).
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
///     Represents a resolved indirect object in the form <c>N G obj ... endobj</c>
///     (ISO 32000-1 §7.3.10). Produced by <see cref="Unchained.Pdf.Parsing.PdfParser.ReadObject" />
///     and cached by <see cref="Unchained.Pdf.Document.PdfDocumentCore" />.
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

    /// <summary>Returns an <see cref="PdfIndirectReference" /> pointing to this object.</summary>
    public PdfIndirectReference ToReference() => new(ObjectNumber, Generation);

    /// <inheritdoc />
    public override string ToString() => $"{ObjectNumber} {Generation} obj ... endobj";
}

/// <summary>
///     Carries a decoded inline image (BI…ID…EI) as a single content-operator operand.
///     Not part of the ISO 32000 object model; used internally to pass decoded image
///     pixels from the content-stream parser to the page renderer.
/// </summary>
internal sealed class PdfInlineImage(
    int width,
    int height,
    byte[] rgbData,
    double userWidth,
    double userHeight
) : PdfObject
{
    internal int Width { get; } = width;
    internal int Height { get; } = height;
    internal byte[] RgbData { get; } = rgbData;

    /// <summary>Image width in PDF user-space points (from the BI /W entry × CTM scale).</summary>
    internal double UserWidth { get; } = userWidth;

    /// <summary>Image height in PDF user-space points (from the BI /H entry × CTM scale).</summary>
    internal double UserHeight { get; } = userHeight;
}
