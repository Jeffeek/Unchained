using System.IO.Compression;

namespace Unchained.Drawing.Encoders;

/// <summary>
/// Encodes a <see cref="RasterBuffer"/> to a baseline JFIF JPEG using only BCL APIs.
/// Uses standard Annex K Huffman tables and a simple DCT implementation.
/// Quality maps to a quantisation scale factor via the standard formula.
/// </summary>
internal static class JpegEncoder
{
    // ── Standard JPEG quantisation tables (Annex K of ISO 10918-1) ──────────

    private static readonly byte[] LumQ =
    [
        16, 11, 10, 16, 24, 40, 51, 61,
        12, 12, 14, 19, 26, 58, 60, 55,
        14, 13, 16, 24, 40, 57, 69, 56,
        14, 17, 22, 29, 51, 87, 80, 62,
        18, 22, 37, 56, 68,109,103, 77,
        24, 35, 55, 64, 81,104,113, 92,
        49, 64, 78, 87,103,121,120,101,
        72, 92, 95, 98,112,100,103, 99
    ];

    private static readonly byte[] ChrQ =
    [
        17, 18, 24, 47, 99, 99, 99, 99,
        18, 21, 26, 66, 99, 99, 99, 99,
        24, 26, 56, 99, 99, 99, 99, 99,
        47, 66, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99
    ];

    // ── Standard Huffman tables (Annex K) ────────────────────────────────────
    // DC luminance
    private static readonly byte[] DcLumBits  = [0,1,5,1,1,1,1,1,1,0,0,0,0,0,0,0];
    private static readonly byte[] DcLumVals  = [0,1,2,3,4,5,6,7,8,9,10,11];
    // DC chrominance
    private static readonly byte[] DcChrBits  = [0,3,1,1,1,1,1,1,1,1,1,0,0,0,0,0];
    private static readonly byte[] DcChrVals  = [0,1,2,3,4,5,6,7,8,9,10,11];
    // AC luminance
    private static readonly byte[] AcLumBits  = [0,2,1,3,3,2,4,3,5,5,4,4,0,0,1,0x7d];
    private static readonly byte[] AcLumVals  =
    [
        0x01,0x02,0x03,0x00,0x04,0x11,0x05,0x12,0x21,0x31,0x41,0x06,0x13,0x51,0x61,
        0x07,0x22,0x71,0x14,0x32,0x81,0x91,0xa1,0x08,0x23,0x42,0xb1,0xc1,0x15,0x52,
        0xd1,0xf0,0x24,0x33,0x62,0x72,0x82,0x09,0x0a,0x16,0x17,0x18,0x19,0x1a,0x25,
        0x26,0x27,0x28,0x29,0x2a,0x34,0x35,0x36,0x37,0x38,0x39,0x3a,0x43,0x44,0x45,
        0x46,0x47,0x48,0x49,0x4a,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5a,0x63,0x64,
        0x65,0x66,0x67,0x68,0x69,0x6a,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7a,0x83,
        0x84,0x85,0x86,0x87,0x88,0x89,0x8a,0x92,0x93,0x94,0x95,0x96,0x97,0x98,0x99,
        0x9a,0xa2,0xa3,0xa4,0xa5,0xa6,0xa7,0xa8,0xa9,0xaa,0xb2,0xb3,0xb4,0xb5,0xb6,
        0xb7,0xb8,0xb9,0xba,0xc2,0xc3,0xc4,0xc5,0xc6,0xc7,0xc8,0xc9,0xca,0xd2,0xd3,
        0xd4,0xd5,0xd6,0xd7,0xd8,0xd9,0xda,0xe1,0xe2,0xe3,0xe4,0xe5,0xe6,0xe7,0xe8,
        0xe9,0xea,0xf1,0xf2,0xf3,0xf4,0xf5,0xf6,0xf7,0xf8,0xf9,0xfa
    ];
    // AC chrominance
    private static readonly byte[] AcChrBits  = [0,2,1,2,4,4,3,4,7,5,4,4,0,1,2,0x77];
    private static readonly byte[] AcChrVals  =
    [
        0x00,0x01,0x02,0x03,0x11,0x04,0x05,0x21,0x31,0x06,0x12,0x41,0x51,0x07,0x61,
        0x71,0x13,0x22,0x32,0x81,0x08,0x14,0x42,0x91,0xa1,0xb1,0xc1,0x09,0x23,0x33,
        0x52,0xf0,0x15,0x62,0x72,0xd1,0x0a,0x16,0x24,0x34,0xe1,0x25,0xf1,0x17,0x18,
        0x19,0x1a,0x26,0x27,0x28,0x29,0x2a,0x35,0x36,0x37,0x38,0x39,0x3a,0x43,0x44,
        0x45,0x46,0x47,0x48,0x49,0x4a,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5a,0x63,
        0x64,0x65,0x66,0x67,0x68,0x69,0x6a,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7a,
        0x82,0x83,0x84,0x85,0x86,0x87,0x88,0x89,0x8a,0x92,0x93,0x94,0x95,0x96,0x97,
        0x98,0x99,0x9a,0xa2,0xa3,0xa4,0xa5,0xa6,0xa7,0xa8,0xa9,0xaa,0xb2,0xb3,0xb4,
        0xb5,0xb6,0xb7,0xb8,0xb9,0xba,0xc2,0xc3,0xc4,0xc5,0xc6,0xc7,0xc8,0xc9,0xca,
        0xd2,0xd3,0xd4,0xd5,0xd6,0xd7,0xd8,0xd9,0xda,0xe2,0xe3,0xe4,0xe5,0xe6,0xe7,
        0xe8,0xe9,0xea,0xf2,0xf3,0xf4,0xf5,0xf6,0xf7,0xf8,0xf9,0xfa
    ];

    // JPEG zig-zag scan order
    private static readonly int[] ZigZag =
    [
         0, 1, 8,16, 9, 2, 3,10,17,24,32,25,18,11, 4, 5,
        12,19,26,33,40,48,41,34,27,20,13, 6, 7,14,21,28,
        35,42,49,56,57,50,43,36,29,22,15,23,30,37,44,51,
        58,59,52,45,38,31,39,46,53,60,61,54,47,55,62,63
    ];

    internal static byte[] Encode(RasterBuffer buffer, int quality = 85)
    {
        quality = Math.Clamp(quality, 1, 100);
        var scale = quality < 50 ? 5000 / quality : 200 - (quality * 2);

        var lumQt  = ScaleQt(LumQ, scale);
        var chrQt  = ScaleQt(ChrQ, scale);

        var (dcLumCodes, dcLumLens) = BuildHuffTable(DcLumBits, DcLumVals);
        var (acLumCodes, acLumLens) = BuildHuffTable(AcLumBits, AcLumVals);
        var (dcChrCodes, dcChrLens) = BuildHuffTable(DcChrBits, DcChrVals);
        var (acChrCodes, acChrLens) = BuildHuffTable(AcChrBits, AcChrVals);

        var w = buffer.Width;
        var h = buffer.Height;
        var src = buffer.ToArgbBytes();

        using var ms = new MemoryStream();

        // ── SOI ─────────────────────────────────────────────────────────────
        ms.WriteByte(0xFF); ms.WriteByte(0xD8);

        // ── APP0 JFIF ────────────────────────────────────────────────────────
        WriteApp0(ms);

        // ── DQT ─────────────────────────────────────────────────────────────
        WriteDqt(ms, lumQt, 0);
        WriteDqt(ms, chrQt, 1);

        // ── SOF0 ─────────────────────────────────────────────────────────────
        WriteSof0(ms, w, h);

        // ── DHT ─────────────────────────────────────────────────────────────
        WriteDht(ms, DcLumBits, DcLumVals, 0, 0);
        WriteDht(ms, AcLumBits, AcLumVals, 1, 0);
        WriteDht(ms, DcChrBits, DcChrVals, 0, 1);
        WriteDht(ms, AcChrBits, AcChrVals, 1, 1);

        // ── SOS + entropy-coded data ─────────────────────────────────────────
        WriteSos(ms);

        // Encode MCUs (8×8 blocks in YCbCr)
        var bw = new BitWriter(ms);
        int dcY = 0, dcCb = 0, dcCr = 0;

        for (var by = 0; by < h; by += 8)
        for (var bx = 0; bx < w; bx += 8)
        {
            var Y  = new double[64];
            var Cb = new double[64];
            var Cr = new double[64];

            for (var dy = 0; dy < 8; dy++)
            for (var dx = 0; dx < 8; dx++)
            {
                var px = Math.Min(bx + dx, w - 1);
                var py = Math.Min(by + dy, h - 1);
                var o  = ((py * w) + px) * 4;
                var r = src[o]; var g = src[o + 1]; var b2 = src[o + 2];
                var idx = (dy * 8) + dx;
                Y[idx]  =  (0.299   * r) + (0.587   * g) + (0.114   * b2) - 128;
                Cb[idx] = -(0.16874 * r) - (0.33126 * g) + (0.5     * b2);
                Cr[idx] =  (0.5     * r) - (0.41869 * g) - (0.08131 * b2);
            }

            EncodeBlock(Y,  lumQt, dcLumCodes, dcLumLens, acLumCodes, acLumLens, ref dcY,  bw);
            EncodeBlock(Cb, chrQt, dcChrCodes, dcChrLens, acChrCodes, acChrLens, ref dcCb, bw);
            EncodeBlock(Cr, chrQt, dcChrCodes, dcChrLens, acChrCodes, acChrLens, ref dcCr, bw);
        }

        bw.Flush();

        // ── EOI ─────────────────────────────────────────────────────────────
        ms.WriteByte(0xFF); ms.WriteByte(0xD9);

        return ms.ToArray();
    }

    // ── Encoding helpers ──────────────────────────────────────────────────────

    private static void EncodeBlock(
        double[] block, int[] qt,
        int[] dcCodes, int[] dcLens,
        int[] acCodes, int[] acLens,
        ref int dcPrev, BitWriter bw)
    {
        // 2D DCT
        var dct = Dct8x8(block);

        // Quantise in zig-zag order
        var quant = new int[64];
        for (var i = 0; i < 64; i++)
            quant[i] = (int)Math.Round(dct[ZigZag[i]] / qt[i]);

        // DC coefficient (differential)
        var dc = quant[0] - dcPrev;
        dcPrev = quant[0];
        WriteHuffInt(dc, dcCodes, dcLens, bw);

        // AC coefficients
        var zeros = 0;
        for (var i = 1; i < 64; i++)
        {
            if (quant[i] == 0) { zeros++; continue; }
            while (zeros >= 16)
            {
                bw.WriteBits(acCodes[0xF0], acLens[0xF0]); // ZRL
                zeros -= 16;
            }
            var sym = (zeros << 4) | SizeOf(quant[i]);
            bw.WriteBits(acCodes[sym], acLens[sym]);
            bw.WriteBits(VlcBits(quant[i]), SizeOf(quant[i]));
            zeros = 0;
        }
        bw.WriteBits(acCodes[0], acLens[0]); // EOB
    }

    private static void WriteHuffInt(int v, int[] codes, int[] lens, BitWriter bw)
    {
        var s = SizeOf(v);
        bw.WriteBits(codes[s], lens[s]);
        if (s > 0) bw.WriteBits(VlcBits(v), s);
    }

    private static int SizeOf(int v)
    {
        if (v == 0) return 0;
        v = Math.Abs(v);
        var s = 0;
        while (v > 0) { v >>= 1; s++; }
        return s;
    }

    private static int VlcBits(int v) => v >= 0 ? v : v + (1 << SizeOf(v)) - 1;

    // ── 8×8 DCT ───────────────────────────────────────────────────────────────
    private static double[] Dct8x8(double[] block)
    {
        var tmp = new double[64];
        var out2 = new double[64];

        // Row pass
        for (var row = 0; row < 8; row++)
        {
            var r = row * 8;
            for (var u = 0; u < 8; u++)
            {
                var sum = 0.0;
                for (var x = 0; x < 8; x++)
                    sum += block[r + x] * Math.Cos(((2 * x + 1) * u * Math.PI) / 16.0);
                tmp[r + u] = (u == 0 ? 1.0 / Math.Sqrt(2) : 1.0) * sum;
            }
        }

        // Column pass
        for (var col = 0; col < 8; col++)
        for (var v = 0; v < 8; v++)
        {
            var sum = 0.0;
            for (var y = 0; y < 8; y++)
                sum += tmp[(y * 8) + col] * Math.Cos(((2 * y + 1) * v * Math.PI) / 16.0);
            out2[(v * 8) + col] = 0.25 * (v == 0 ? 1.0 / Math.Sqrt(2) : 1.0) * sum;
        }

        return out2;
    }

    // ── Huffman table builder ─────────────────────────────────────────────────
    private static (int[] Codes, int[] Lens) BuildHuffTable(byte[] bits, byte[] vals)
    {
        var codes = new int[256];
        var lens  = new int[256];
        var code = 0;
        var idx  = 0;
        for (var len = 1; len <= 16; len++)
        {
            for (var i = 0; i < bits[len - 1]; i++)
            {
                codes[vals[idx]] = code;
                lens[vals[idx]]  = len;
                code++; idx++;
            }
            code <<= 1;
        }
        return (codes, lens);
    }

    // ── Quantisation table scaling ────────────────────────────────────────────
    private static int[] ScaleQt(byte[] qt, int scale)
    {
        var result = new int[64];
        for (var i = 0; i < 64; i++)
            result[i] = Math.Clamp((qt[i] * scale + 50) / 100, 1, 255);
        return result;
    }

    // ── JPEG segment writers ──────────────────────────────────────────────────
    private static void WriteApp0(Stream s)
    {
        s.WriteByte(0xFF); s.WriteByte(0xE0);
        var seg = new byte[] { 0,16, 0x4A,0x46,0x49,0x46,0x00, 1,1, 0, 0,1, 0,1, 0,0 };
        s.Write(seg);
    }

    private static void WriteDqt(Stream s, int[] qt, int id)
    {
        s.WriteByte(0xFF); s.WriteByte(0xDB);
        WriteU16(s, 67); // length = 2 + 1 + 64
        s.WriteByte((byte)(0 | id)); // precision 0 = 8-bit
        for (var i = 0; i < 64; i++)
            s.WriteByte((byte)qt[ZigZag[i]]);
    }

    private static void WriteSof0(Stream s, int w, int h)
    {
        s.WriteByte(0xFF); s.WriteByte(0xC0);
        WriteU16(s, 17); // 2+1+2+2+1+3×3
        s.WriteByte(8);  // precision
        WriteU16(s, h);
        WriteU16(s, w);
        s.WriteByte(3);  // components: Y, Cb, Cr
        // Component: id, sampling, qt id
        s.WriteByte(1); s.WriteByte(0x11); s.WriteByte(0); // Y
        s.WriteByte(2); s.WriteByte(0x11); s.WriteByte(1); // Cb
        s.WriteByte(3); s.WriteByte(0x11); s.WriteByte(1); // Cr
    }

    private static void WriteDht(Stream s, byte[] bits, byte[] vals, int acDc, int id)
    {
        s.WriteByte(0xFF); s.WriteByte(0xC4);
        WriteU16(s, (ushort)(3 + bits.Length + vals.Length));
        s.WriteByte((byte)((acDc << 4) | id));
        s.Write(bits);
        s.Write(vals);
    }

    private static void WriteSos(Stream s)
    {
        s.WriteByte(0xFF); s.WriteByte(0xDA);
        WriteU16(s, 12);
        s.WriteByte(3);
        s.WriteByte(1); s.WriteByte(0x00); // Y  → DC0, AC0
        s.WriteByte(2); s.WriteByte(0x11); // Cb → DC1, AC1
        s.WriteByte(3); s.WriteByte(0x11); // Cr → DC1, AC1
        s.WriteByte(0); s.WriteByte(63); s.WriteByte(0);
    }

    private static void WriteU16(Stream s, int v)
    {
        s.WriteByte((byte)(v >> 8));
        s.WriteByte((byte)v);
    }

    // ── Bit-level writer (stuffs 0x00 after 0xFF per JPEG spec) ─────────────
    private sealed class BitWriter(Stream stream)
    {
        private uint _buf;
        private int  _bits;

        internal void WriteBits(int code, int len)
        {
            _buf  = (_buf << len) | (uint)code;
            _bits += len;
            while (_bits >= 8)
            {
                _bits -= 8;
                var b = (byte)(_buf >> _bits);
                stream.WriteByte(b);
                if (b == 0xFF) stream.WriteByte(0x00); // byte stuffing
            }
        }

        internal void Flush()
        {
            if (_bits <= 0) return;
            var b = (byte)((_buf << (8 - _bits)) | ((1 << (8 - _bits)) - 1));
            stream.WriteByte(b);
            if (b == 0xFF) stream.WriteByte(0x00);
        }
    }
}
