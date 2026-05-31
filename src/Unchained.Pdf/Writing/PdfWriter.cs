using System.Buffers;
using System.Text;
using Unchained.Pdf.Core;

namespace Unchained.Pdf.Writing;

/// <summary>
/// Serializes a graph of <see cref="PdfObject"/> instances to a standards-compliant
/// PDF byte stream (ISO 32000-1 §7.3 + §7.5).
/// <para>
/// Write order:
/// <list type="number">
///   <item>Header — <c>%PDF-1.7</c> + binary-content marker (§7.5.2).</item>
///   <item>Body — each <see cref="PdfIndirectObject"/> at a tracked byte offset.</item>
///   <item>Cross-reference table — maps object numbers to byte offsets (§7.5.4).</item>
///   <item>Trailer — trailer dictionary + <c>startxref</c> + <c>%%EOF</c> (§7.5.5).</item>
/// </list>
/// All output is written to the <see cref="IBufferWriter{T}"/> supplied at construction,
/// keeping the serializer allocation-free on the hot path.
/// </para>
/// </summary>
internal sealed class PdfWriter(IBufferWriter<byte> output) : IDisposable
{
    // Tracks the current byte position so that xref offsets can be recorded accurately.
    private long _position;

    // Maps object number → byte offset of the corresponding "N G obj" header.
    private readonly Dictionary<int, long> _objectOffsets = new();

    // ── High-level document write ─────────────────────────────────────────────

    /// <summary>
    /// Writes a complete, structurally valid PDF document.
    /// The caller is responsible for populating the trailer dictionary with at minimum
    /// <c>/Size</c> and <c>/Root</c> before calling this method.
    /// </summary>
    /// <param name="objects">
    /// Ordered list of all indirect objects to include in the document body.
    /// Object numbers must be unique and non-zero.
    /// </param>
    /// <param name="trailer">
    /// The trailer dictionary. Must contain at least <c>/Size</c> (total number of
    /// objects + 1) and <c>/Root</c> (reference to the document catalog).
    /// </param>
    public void Write(IReadOnlyList<PdfIndirectObject> objects, PdfDictionary trailer)
    {
        WriteHeader();
        foreach (var obj in objects)
            WriteIndirectObject(obj);
        var xrefOffset = _position;
        // Max object number determines the xref section size.
        // Objects may not be consecutively numbered when re-serializing a loaded document.
        var maxObjNum = objects.Count > 0 ? objects.Max(static o => o.ObjectNumber) : 0;
        WriteXrefTable(maxObjNum);
        WriteTrailer(trailer, xrefOffset);
    }

    // ── Header ────────────────────────────────────────────────────────────────

    // §7.5.2 — The header line followed by a comment containing four bytes > 127.
    // The high-byte comment signals to file-transfer tools that the file is binary.
    private void WriteHeader()
    {
        WriteBytes("%PDF-1.7\n"u8);
        WriteBytes("%\xE2\xE3\xCF\xD3"u8 + "\n"u8);
    }

    // ── Object serialization ──────────────────────────────────────────────────

    /// <summary>
    /// Writes a single <c>N G obj ... endobj</c> block and records its byte offset
    /// in the internal offset table for use in the cross-reference section.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public void WriteIndirectObject(PdfIndirectObject obj)
    {
        _objectOffsets[obj.ObjectNumber] = _position;
        WriteAscii($"{obj.ObjectNumber} {obj.Generation} obj\n");
        WriteValue(obj.Value);
        WriteBytes("\nendobj\n"u8);
    }

    /// <summary>
    /// Writes the PDF syntax representation of <paramref name="obj"/> to the output.
    /// Supports all eight PDF object types plus <see cref="PdfIndirectReference"/>.
    /// </summary>
    /// <exception cref="PdfException">
    /// Thrown when <paramref name="obj"/> is of an unrecognized type that has no
    /// PDF syntax representation.
    /// </exception>
    // ReSharper disable once MemberCanBePrivate.Global
    public void WriteValue(PdfObject obj)
    {
        switch (obj)
        {
            case PdfBoolean b: WriteBytes(b.Value ? "true"u8 : "false"u8); break;
            case PdfInteger i: WriteAscii(i.Value.ToString()); break;
            case PdfReal r: WriteAscii(r.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture)); break;
            case PdfNull: WriteBytes("null"u8); break;
            case PdfName n: WriteName(n); break;
            case PdfString s: WriteString(s); break;
            case PdfArray a: WriteArray(a); break;
            case PdfDictionary d: WriteDictionary(d); break;
            case PdfStream s: WriteStream(s); break;
            case PdfIndirectReference r: WriteAscii($"{r.ObjectNumber} {r.Generation} R"); break;
            default: throw new PdfException($"Cannot serialize {obj.GetType().Name}.");
        }
    }

    // Writes "/Name" with '#xx' escaping for bytes that require it (§7.3.5).
    private void WriteName(PdfName name)
    {
        WriteBytes("/"u8);
        var span = Encoding.Latin1.GetBytes(name.Value).AsSpan();
        foreach (var b in span)
        {
            if (NeedsNameEscape(b))
                WriteAscii($"#{b:X2}");
            else
                WriteByte(b);
        }
    }

    // Writes a string as either <hex> or (literal) depending on IsHex.
    private void WriteString(PdfString str)
    {
        if (str.IsHex)
        {
            WriteBytes("<"u8);
            foreach (var b in str.Bytes.Span)
                WriteAscii(b.ToString("X2"));
            WriteBytes(">"u8);
        }
        else
        {
            WriteBytes("("u8);
            foreach (var b in str.Bytes.Span)
            {
                if (b is (byte)'(' or (byte)')' or (byte)'\\')
                    WriteByte((byte)'\\');
                WriteByte(b);
            }

            WriteBytes(")"u8);
        }
    }

    private void WriteArray(PdfArray array)
    {
        WriteBytes("["u8);
        for (var i = 0; i < array.Count; i++)
        {
            if (i > 0) WriteBytes(" "u8);
            WriteValue(array[i]);
        }

        WriteBytes("]"u8);
    }

    private void WriteDictionary(PdfDictionary dict)
    {
        WriteBytes("<<"u8);
        foreach (var (key, value) in dict.Entries)
        {
            WriteBytes("\n"u8);
            WriteName(PdfName.Get(key));
            WriteBytes(" "u8);
            WriteValue(value);
        }

        WriteBytes("\n>>"u8);
    }

    private void WriteStream(PdfStream stream)
    {
        WriteDictionary(stream.Dictionary);
        WriteBytes("\nstream\n"u8);
        WriteBytes(stream.Data.Span);
        WriteBytes("\nendstream"u8);
    }

    // ── Cross-reference table ─────────────────────────────────────────────────

    // Writes the traditional xref section (§7.5.4).
    // Covers objects 0..<paramref name="maxObjectNumber"/> inclusive.
    // Objects with no recorded offset are written as free entries so the
    // xref section is contiguous even when object numbers have gaps.
    private void WriteXrefTable(int maxObjectNumber)
    {
        var count = maxObjectNumber + 1;
        WriteBytes("xref\n"u8);
        WriteAscii($"0 {count}\n");
        WriteAscii("0000000000 65535 f \r\n"); // free object 0
        for (var i = 1; i <= maxObjectNumber; i++)
        {
            WriteAscii(_objectOffsets.TryGetValue(i, out var offset)
                ? $"{offset:D10} 00000 n \r\n"
                : "0000000000 00000 f \r\n"); // gap — mark as free
        }
    }

    // ── Trailer ───────────────────────────────────────────────────────────────

    private void WriteTrailer(PdfDictionary trailer, long xrefOffset)
    {
        WriteBytes("trailer\n"u8);
        WriteDictionary(trailer);
        WriteBytes("\nstartxref\n"u8);
        WriteAscii(xrefOffset.ToString());
        WriteBytes("\n%%EOF\n"u8);
    }

    // ── Low-level output ──────────────────────────────────────────────────────

    private void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        var span = output.GetSpan(bytes.Length);
        bytes.CopyTo(span);
        output.Advance(bytes.Length);
        _position += bytes.Length;
    }

    private void WriteByte(byte b)
    {
        var span = output.GetSpan(1);
        span[0] = b;
        output.Advance(1);
        _position++;
    }

    private void WriteAscii(string s)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        WriteBytes(bytes);
    }

    // §7.3.5 — bytes that must be escaped with '#xx' in a name token.
    private static bool NeedsNameEscape(byte b) =>
        b is <= 0x20 or >= 0x7F or (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or
            (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or
            (byte)'/' or (byte)'%' or (byte)'#';

    /// <inheritdoc />
    public void Dispose() { }
}
