using Unchained.Drawing.Constants;

namespace Unchained.Drawing.Tests.Decoders;

/// <summary>
///     A minimal baseline grayscale (1-component) JFIF JPEG writer used only by the decoder tests, to
///     exercise the decoder's grayscale-upsample and restart-interval paths that the production RGB
///     encoder (which only emits 3-component 4:4:4) cannot reach. Uses the standard Annex K luma
///     Huffman tables and a flat quantisation table.
/// </summary>
internal static class GrayscaleJpegBuilder
{
    private static readonly byte[] DcLumBits = [0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] DcLumVals = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
    private static readonly byte[] AcLumBits = [0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d];
    private static readonly byte[] AcLumVals =
    [
        0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61,
        0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08, 0x23, 0x42, 0xb1, 0xC1, 0x15, 0x52,
        0xd1, 0xF0, 0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x25,
        0x26, 0x27, 0x28, 0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45,
        0x46, 0x47, 0x48, 0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x63, 0x64,
        0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x83,
        0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99,
        0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6,
        0xb7, 0xb8, 0xb9, 0xba, 0xC2, 0xc3, 0xC4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3,
        0xd4, 0xd5, 0xd6, 0xD7, 0xD8, 0xD9, 0xDA, 0xe1, 0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8,
        0xe9, 0xea, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa
    ];

    /// <summary>
    ///     Builds a grayscale baseline JPEG of the given dimensions (rounded up internally to whole
    ///     8×8 blocks). <paramref name="restartInterval" /> > 0 inserts a DRI segment and RSTn markers
    ///     between MCU runs. <paramref name="gray" /> is the flat luma value for every pixel.
    /// </summary>
    // ReSharper disable once BadListLineBreaks
    public static byte[] Build(int width, int height, byte gray = 128, int restartInterval = 0)
    {
        var qt = new int[64];
        Array.Fill(qt, 1); // identity quantisation → exact DC.

        var (dcCodes, dcLens) = BuildHuffTable(DcLumBits, DcLumVals);
        var (acCodes, acLens) = BuildHuffTable(AcLumBits, AcLumVals);

        using var ms = new MemoryStream();
        ms.WriteByte(JpegConstants.MarkerPrefix);
        ms.WriteByte(JpegConstants.Soi);

        WriteApp0(ms);
        WriteDqt(ms, qt);
        WriteSof0(ms, width, height);
        WriteDht(ms, DcLumBits, DcLumVals, acDc: 0, id: 0);
        WriteDht(ms, AcLumBits, AcLumVals, acDc: 1, id: 0);
        if (restartInterval > 0) WriteDri(ms, restartInterval);

        WriteSos(ms);

        var mcusX = (width + 7) / 8;
        var mcusY = (height + 7) / 8;
        var dcPrev = 0;
        var bw = new BitWriter(ms);
        var mcuCount = 0;

        for (var my = 0; my < mcusY; my++)
        for (var mx = 0; mx < mcusX; mx++)
        {
            var block = new double[64];
            Array.Fill(block, gray - 128.0);
            EncodeBlock(
                block,
                qt,
                dcCodes,
                dcLens,
                acCodes,
                acLens,
                ref dcPrev,
                bw
            );

            mcuCount++;
            if (restartInterval <= 0 || mcuCount % restartInterval != 0 || (my == mcusY - 1 && mx == mcusX - 1))
                continue;

            bw.Flush();
            var rst = (byte)(JpegConstants.RstFirst + (((mcuCount / restartInterval) - 1) % 8));
            ms.WriteByte(JpegConstants.MarkerPrefix);
            ms.WriteByte(rst);
            dcPrev = 0;
        }

        bw.Flush();
        ms.WriteByte(JpegConstants.MarkerPrefix);
        ms.WriteByte(JpegConstants.Eoi);
        return ms.ToArray();
    }

    private static void EncodeBlock(
        IReadOnlyList<double> block,
        IReadOnlyList<int> qt,
        IReadOnlyList<int> dcCodes,
        IReadOnlyList<int> dcLens,
        IReadOnlyList<int> acCodes,
        IReadOnlyList<int> acLens,
        ref int dcPrev,
        BitWriter bw
    )
    {
        var dct = Dct8X8(block);
        var quant = new int[64];
        for (var i = 0; i < 64; i++)
            quant[i] = (int)Math.Round(dct[JpegConstants.ZigZag[i]] / qt[i]);

        var dc = quant[0] - dcPrev;
        dcPrev = quant[0];
        WriteHuffInt(dc, dcCodes, dcLens, bw);

        var zeros = 0;
        for (var i = 1; i < 64; i++)
        {
            if (quant[i] == 0)
            {
                zeros++;
                continue;
            }

            while (zeros >= 16)
            {
                bw.WriteBits(acCodes[JpegConstants.ZrlAcCode], acLens[JpegConstants.ZrlAcCode]);
                zeros -= 16;
            }

            var sym = (zeros << 4) | SizeOf(quant[i]);
            bw.WriteBits(acCodes[sym], acLens[sym]);
            bw.WriteBits(VlcBits(quant[i]), SizeOf(quant[i]));
            zeros = 0;
        }

        bw.WriteBits(acCodes[0], acLens[0]); // EOB
    }

    private static void WriteHuffInt(
        int v,
        IReadOnlyList<int> codes,
        IReadOnlyList<int> lens,
        BitWriter bw
    )
    {
        var s = SizeOf(v);
        bw.WriteBits(codes[s], lens[s]);
        if (s > 0)
            bw.WriteBits(VlcBits(v), s);
    }

    private static int SizeOf(int v)
    {
        if (v == 0) return 0;

        v = Math.Abs(v);
        var s = 0;
        while (v > 0)
        {
            v >>= 1;
            s++;
        }

        return s;
    }

    private static int VlcBits(int v) => v >= 0 ? v : v + (1 << SizeOf(v)) - 1;

    private static double[] Dct8X8(IReadOnlyList<double> block)
    {
        var tmp = new double[64];
        var output = new double[64];
        for (var row = 0; row < 8; row++)
        {
            var r = row * 8;
            for (var u = 0; u < 8; u++)
            {
                var sum = 0d;
                for (var x = 0; x < 8; x++)
                    sum += block[r + x] * Math.Cos(((2 * x) + 1) * (double)u * Math.PI / 16d);
                tmp[r + u] = (u == 0 ? 1.0 / Math.Sqrt(2) : 1d) * sum;
            }
        }

        for (var col = 0; col < 8; col++)
        for (var v = 0; v < 8; v++)
        {
            var sum = 0d;
            for (var y = 0; y < 8; y++)
                sum += tmp[(y * 8) + col] * Math.Cos(((2 * y) + 1) * (double)v * Math.PI / 16d);
            output[(v * 8) + col] = 0.25 * (v == 0 ? 1d / Math.Sqrt(2) : 1d) * sum;
        }

        return output;
    }

    private static (int[] Codes, int[] Lens) BuildHuffTable(IReadOnlyList<byte> bits, IReadOnlyList<byte> vals)
    {
        var codes = new int[256];
        var lens = new int[256];
        var code = 0;
        var idx = 0;
        for (var len = 1; len <= 16; len++)
        {
            for (var i = 0; i < bits[len - 1]; i++)
            {
                codes[vals[idx]] = code;
                lens[vals[idx]] = len;
                code++;
                idx++;
            }

            code <<= 1;
        }

        return (codes, lens);
    }

    private static void WriteApp0(Stream s)
    {
        s.WriteByte(JpegConstants.MarkerPrefix);
        s.WriteByte(JpegConstants.App0Jfif);
        s.Write([0, 16, 0x4A, 0x46, 0x49, 0x46, 0x00, 1, 1, 0, 0, 1, 0, 1, 0, 0]);
    }

    private static void WriteDqt(Stream s, IReadOnlyList<int> qt)
    {
        s.WriteByte(JpegConstants.MarkerPrefix);
        s.WriteByte(JpegConstants.Dqt);
        WriteU16(s, 67);
        s.WriteByte(0);
        for (var i = 0; i < 64; i++)
            s.WriteByte((byte)qt[JpegConstants.ZigZag[i]]);
    }

    private static void WriteSof0(Stream s, int w, int h)
    {
        s.WriteByte(JpegConstants.MarkerPrefix);
        s.WriteByte(JpegConstants.Sof0);
        WriteU16(s, 11); // 2+1+2+2+1+1×3
        s.WriteByte(8);
        WriteU16(s, h);
        WriteU16(s, w);
        s.WriteByte(1);
        s.WriteByte(1);
        s.WriteByte(JpegConstants.SamplingFactor1X1);
        s.WriteByte(0);
    }

    private static void WriteDht(
        Stream s,
        byte[] bits,
        byte[] vals,
        int acDc,
        int id
    )
    {
        s.WriteByte(JpegConstants.MarkerPrefix);
        s.WriteByte(JpegConstants.Dht);
        WriteU16(s, 3 + bits.Length + vals.Length);
        s.WriteByte((byte)((acDc << 4) | id));
        s.Write(bits);
        s.Write(vals);
    }

    private static void WriteDri(Stream s, int interval)
    {
        s.WriteByte(JpegConstants.MarkerPrefix);
        s.WriteByte(JpegConstants.Dri);
        WriteU16(s, 4);
        WriteU16(s, interval);
    }

    private static void WriteSos(Stream s)
    {
        s.WriteByte(JpegConstants.MarkerPrefix);
        s.WriteByte(JpegConstants.Sos);
        WriteU16(s, 8);
        s.WriteByte(1);
        s.WriteByte(1);
        s.WriteByte(0); // DC0/AC0
        s.WriteByte(0);
        s.WriteByte(63);
        s.WriteByte(0);
    }

    private static void WriteU16(Stream s, int v)
    {
        s.WriteByte((byte)(v >> 8));
        s.WriteByte((byte)v);
    }

    private sealed class BitWriter(Stream stream)
    {
        private int _bits;
        private uint _buf;

        internal void WriteBits(int code, int len)
        {
            _buf = (_buf << len) | (uint)code;
            _bits += len;
            while (_bits >= 8)
            {
                _bits -= 8;
                var b = (byte)(_buf >> _bits);
                stream.WriteByte(b);
                if (b == JpegConstants.MarkerPrefix)
                    stream.WriteByte(JpegConstants.ByteStuff);
            }
        }

        internal void Flush()
        {
            if (_bits <= 0)
            {
                _buf = 0;
                return;
            }

            var b = (byte)((_buf << (8 - _bits)) | (uint)((1 << (8 - _bits)) - 1));
            stream.WriteByte(b);
            if (b == JpegConstants.MarkerPrefix)
                stream.WriteByte(JpegConstants.ByteStuff);

            _bits = 0;
            _buf = 0;
        }
    }
}
