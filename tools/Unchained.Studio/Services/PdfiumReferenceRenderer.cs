using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using PDFiumCore;
using Unchained.Drawing.Constants;
using Unchained.Drawing.Primitives.Extensions;

namespace Unchained.Studio.Services;

/// <summary>
///     Reference renderer backed by Pdfium — the same C++ engine used by Chrome, Android,
///     iOS WebView.  Runs entirely server-side; no browser, no CDN, no JS timeouts.
/// </summary>
public sealed class PdfiumReferenceRenderer
{
    private static readonly Lock InitLock = new();
    private static bool _initialized;
    private static bool _available;

    /// <summary>True when the Pdfium native library loaded successfully.</summary>
    public bool IsAvailable => EnsureInit();

    private static bool EnsureInit()
    {
        if (_initialized) return _available;

        InitLock.Enter();

        try
        {
            try
            {
                fpdfview.FPDF_InitLibrary();
                _available = true;
            }
            catch
            {
                _available = false;
            }

            _initialized = true;

            return _available;
        }
        finally
        {
            InitLock.Exit();
        }
    }

    /// <summary>
    ///     Renders one page to a PNG byte array using Pdfium.
    ///     Returns <see langword="null" /> when Pdfium is unavailable or the page fails.
    /// </summary>
    public async Task<byte[]?> RenderPageAsync(
        byte[] pdfBytes,
        int pageNumber,
        int dpi = 96,
        CancellationToken ct = default
    ) =>
        !EnsureInit()
            ? null
            : await Task.Run(() => RenderPage(pdfBytes, pageNumber, dpi), ct).ConfigureAwait(false);

    // ── Core render path ──────────────────────────────────────────────────────

    private static byte[]? RenderPage(IReadOnlyCollection<byte> pdfBytes, int pageNumber, int dpi)
    {
        FpdfDocumentT? doc = null;
        FpdfPageT? page = null;
        FpdfBitmapT? bitmap = null;

        // Pin the byte array so Pdfium can read it via IntPtr without GC relocation.
        var gch = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        try
        {
            // Load document from memory — requires pinned IntPtr
            doc = fpdfview.FPDF_LoadMemDocument(gch.AddrOfPinnedObject(), pdfBytes.Count, null);
            gch.Free(); // document is now loaded; we no longer need the pin
            if (doc is null) return null;

            var pageCount = fpdfview.FPDF_GetPageCount(doc);
            if (pageNumber < 1 || pageNumber > pageCount) return null;

            // Load page (0-based index)
            page = fpdfview.FPDF_LoadPage(doc, pageNumber - 1);
            if (page is null) return null;

            // Compute pixel dimensions
            var widthPt = fpdfview.FPDF_GetPageWidthF(page);
            var heightPt = fpdfview.FPDF_GetPageHeightF(page);
            var scale = dpi / 72.0;
            var pixW = Math.Max(1, (int)Math.Ceiling(widthPt * scale));
            var pixH = Math.Max(1, (int)Math.Ceiling(heightPt * scale));

            // Create BGRA bitmap, fill with white, render
            bitmap = fpdfview.FPDFBitmapCreate(pixW, pixH, 0 /* no alpha */);
            if (bitmap is null) return null;

            fpdfview.FPDFBitmapFillRect(bitmap,
                0,
                0,
                pixW,
                pixH,
                0xFFFFFFFF);

            // FPDF_ANNOT = 0x01 — also render annotations (matches Chrome's default view)
            fpdfview.FPDF_RenderPageBitmap(bitmap,
                page,
                0,
                0,
                pixW,
                pixH,
                0,
                0x01);

            // Read BGRA pixel data and encode to PNG
            var bufferPtr = fpdfview.FPDFBitmapGetBuffer(bitmap);
            var stride = fpdfview.FPDFBitmapGetStride(bitmap);
            return BgraToPng(bufferPtr, stride, pixW, pixH);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (gch.IsAllocated) gch.Free(); // safety: free if exception before explicit Free
            if (bitmap is not null) fpdfview.FPDFBitmapDestroy(bitmap);
            if (page is not null) fpdfview.FPDF_ClosePage(page);
            if (doc is not null) fpdfview.FPDF_CloseDocument(doc);
        }
    }

    // ── BGRA → PNG ────────────────────────────────────────────────────────────
    // Copies Pdfium's unmanaged BGRA buffer, converts to RGB, and writes a PNG
    // using ZLibStream (BCL, cross-platform — same approach as PdfPngEncoder
    // in Unchained.Pdf.Rendering).

    private static byte[] BgraToPng(
        IntPtr scan0,
        int stride,
        int width,
        int height
    )
    {
        var bgra = new byte[stride * height];
        Marshal.Copy(scan0, bgra, 0, bgra.Length);

        // Convert BGRA → RGB (Pdfium pixel order: B G R A; PNG wants R G B)
        var rgb = new byte[width * height * 3];
        for (var row = 0; row < height; row++)
        for (var col = 0; col < width; col++)
        {
            var src = (row * stride) + (col * 4);
            var dst = ((row * width) + col) * 3;
            rgb[dst] = bgra[src + 2];     // R
            rgb[dst + 1] = bgra[src + 1]; // G
            rgb[dst + 2] = bgra[src];     // B
        }

        return EncodeRgbPng(rgb, width, height);
    }

    private static byte[] EncodeRgbPng(byte[] rgb, int width, int height)
    {
        using var ms = new MemoryStream((width * height * 3) + 256);
        ms.Write(PngConstants.Signature);
        WriteIhdr(ms, width, height);
        WriteIdat(ms, rgb, width, height);
        WriteChunk(ms, PngConstants.IEND.ToUtf8Span(), ReadOnlySpan<byte>.Empty);
        return ms.ToArray();
    }

    private static void WriteIhdr(Stream s, int w, int h)
    {
        Span<byte> d = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(d, w);
        BinaryPrimitives.WriteInt32BigEndian(d[4..], h);
        d[8] = 8;
        d[9] = 2; // bit depth=8, colour type=RGB
        WriteChunk(s, PngConstants.IHDR.ToUtf8Span(), d);
    }

    private static void WriteIdat(
        Stream s,
        byte[] rgb,
        int w,
        int h
    )
    {
        var scanlineStride = w * 3;
        var raw = new byte[h * (1 + scanlineStride)];
        for (var y = 0; y < h; y++)
            Buffer.BlockCopy(rgb, y * scanlineStride, raw, (y * (1 + scanlineStride)) + 1, scanlineStride);
        // raw[y * (1+stride)] = 0 (filter=None) — already 0 from array init

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true))
            zlib.Write(raw);
        WriteChunk(s, PngConstants.IDAT.ToUtf8Span(), compressed.ToArray());
    }

    private static void WriteChunk(Stream s, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
        s.Write(len);
        s.Write(type);
        if (data.Length > 0) s.Write(data);
        var crc = UpdateCrc(0xffffffff, type);
        crc = UpdateCrc(crc, data) ^ 0xffffffff;
        Span<byte> crcBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuf, crc);
        s.Write(crcBuf);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data) crc = PngConstants.CtcTable[(crc ^ b) & JpegConstants.MarkerPrefix] ^ (crc >> 8);
        return crc;
    }
}
