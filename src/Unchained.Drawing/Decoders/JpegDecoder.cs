using Unchained.Drawing.Constants;

namespace Unchained.Drawing.Decoders;

/// <summary>
///     Minimal baseline (sequential DCT, Huffman) JPEG decoder using only BCL APIs, producing
///     packed 24-bit RGB. Supports the common case for slide imagery: baseline JFIF, 8-bit,
///     1-component (grayscale) or 3-component (YCbCr) with 4:4:4 / 4:2:0 / 4:2:2 subsampling.
///     Returns <see langword="null" /> for progressive, arithmetic-coded, CMYK, or otherwise
///     unsupported streams so the caller can skip the image rather than crash.
/// </summary>
internal static class JpegDecoder
{
    internal static byte[]? TryDecodeToRgb(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            var decoder = new Decoder(data);
            return decoder.Decode(out width, out height);
        }
        catch
        {
            return null;
        }
    }

    private sealed class Decoder(ReadOnlySpan<byte> data)
    {
        private readonly byte[] _data = data.ToArray();

        // Huffman tables: [class(0=DC,1=AC)][id]
        private readonly HuffTable?[,] _huff = new HuffTable?[2, 4];

        // Quantization tables [id] → 64 entries (zig-zag order stored, applied in natural order).
        private readonly int[]?[] _quant = new int[4][];

        // Bit reader state for entropy-coded segment.
        private int _bitBuffer;
        private int _bitCount;
        private Component[] _components = [];
        private int _pos;
        private int _restartInterval;

        private int _width, _height;

        internal byte[]? Decode(out int width, out int height)
        {
            width = 0;
            height = 0;

            if (_data.Length < 2 || _data[0] != JpegConstants.MarkerPrefix || _data[1] != JpegConstants.Soi) // SOI
                return null;

            _pos = 2;

            var baseline = false;

            while (_pos + 1 < _data.Length)
            {
                if (_data[_pos] != JpegConstants.MarkerPrefix)
                {
                    _pos++;
                    continue;
                }

                // Skip fill bytes (JpegMarkers.MarkerPrefix JpegMarkers.MarkerPrefix ...).
                while (_pos + 1 < _data.Length && _data[_pos + 1] == JpegConstants.MarkerPrefix)
                    _pos++;

                var marker = _data[_pos + 1];
                _pos += 2;

                switch (marker)
                {
                    case JpegConstants.ByteStuff:                                // stuffed byte outside scan — skip
                    case >= JpegConstants.RstFirst and <= JpegConstants.RstLast: // RSTn (no payload)
                    {
                        break;
                    }
                    case JpegConstants.Eoi:
                    case JpegConstants.Sof2: // SOF2 progressive — unsupported
                    {
                        return null;
                    }
                    case JpegConstants.Sof0: // SOF0 baseline
                    case JpegConstants.Sof1: // SOF1 extended sequential (same layout, Huffman) — treat as baseline
                    {
                        baseline = true;
                        ReadStartOfFrame();
                        break;
                    }
                    case JpegConstants.Dht:
                    {
                        ReadHuffmanTables();
                        break;
                    }
                    case JpegConstants.Dqt:
                    {
                        ReadQuantTables();
                        break;
                    }
                    case JpegConstants.Dri:
                    {
                        ReadRestartInterval();
                        break;
                    }
                    case JpegConstants.Sos:
                    {
                        if (!baseline)
                            return null;

                        var rgb = ReadScanAndDecode();
                        width = _width;
                        height = _height;

                        return rgb;
                    }
                    default:
                    {
                        SkipSegment();
                        break;
                    }
                }
            }

            return null;
        }

        private int ReadU16()
        {
            var v = (_data[_pos] << 8) | _data[_pos + 1];
            _pos += 2;
            return v;
        }

        private void SkipSegment()
        {
            var len = ReadU16();
            _pos += len - 2;
        }

        private void ReadStartOfFrame()
        {
            var len = ReadU16();
            var end = _pos + len - 2;
            var precision = _data[_pos++];
            if (precision != 8)
            {
                _pos = end;
                throw new NotSupportedException("only 8-bit");
            }

            _height = ReadU16();
            _width = ReadU16();

            var count = _data[_pos++];
            if (count != 1 && count != 3)
            {
                _pos = end;
                throw new NotSupportedException("components");
            }

            _components = new Component[count];
            for (var i = 0; i < count; i++)
            {
                var id = _data[_pos++];
                var sampling = _data[_pos++];
                var qt = _data[_pos++];
                _components[i] = new Component
                {
                    Id = id,
                    HSamp = sampling >> 4,
                    VSamp = sampling & JpegConstants.NibbleMask,
                    QuantId = qt
                };
            }

            _pos = end;
        }

        private void ReadQuantTables()
        {
            var len = ReadU16();
            var end = _pos + len - 2;
            while (_pos < end)
            {
                var pqTq = _data[_pos++];
                var precision = pqTq >> 4;
                var id = pqTq & JpegConstants.NibbleMask;
                var table = new int[64];
                for (var i = 0; i < 64; i++)
                    table[i] = precision == 0 ? _data[_pos++] : ReadU16();

                _quant[id] = table;
            }

            _pos = end;
        }

        private void ReadHuffmanTables()
        {
            var len = ReadU16();
            var end = _pos + len - 2;
            while (_pos < end)
            {
                var tcTh = _data[_pos++];
                var cls = tcTh >> 4; // 0 = DC, 1 = AC
                var id = tcTh & JpegConstants.NibbleMask;
                var counts = new byte[16];
                var total = 0;
                for (var i = 0; i < 16; i++)
                {
                    counts[i] = _data[_pos++];
                    total += counts[i];
                }

                var symbols = new byte[total];
                for (var i = 0; i < total; i++)
                    symbols[i] = _data[_pos++];

                _huff[cls, id] = new HuffTable(counts, symbols);
            }

            _pos = end;
        }

        private void ReadRestartInterval()
        {
            ReadU16();
            _restartInterval = ReadU16();
        }

        private byte[]? ReadScanAndDecode()
        {
            var len = ReadU16();
            var end = _pos + len - 2;
            var ns = _data[_pos++];
            var scanComps = new (int CompIndex, int DcId, int AcId)[ns];

            for (var i = 0; i < ns; i++)
            {
                var cs = _data[_pos++];
                var tdTa = _data[_pos++];
                var ci = Array.FindIndex(_components, c => c.Id == cs);
                if (ci < 0)
                {
                    _pos = end;
                    return null;
                }

                scanComps[i] = (ci, tdTa >> 4, tdTa & JpegConstants.NibbleMask);
            }

            // The 3 spectral-selection bytes (Ss/Se/Ah-Al) are within the segment length.
            _pos = end;

            return DecodeBaseline(scanComps);
        }

        private byte[]? DecodeBaseline((int CompIndex, int DcId, int AcId)[] scanComps)
        {
            var hMax = _components.Max(static c => c.HSamp);
            var vMax = _components.Max(static c => c.VSamp);
            var mcuW = 8 * hMax;
            var mcuH = 8 * vMax;
            var mcusX = (_width + mcuW - 1) / mcuW;
            var mcusY = (_height + mcuH - 1) / mcuH;

            foreach (var c in _components)
            {
                c.BlocksPerLine = mcusX * c.HSamp;
                c.Pixels = new byte[mcusX * c.HSamp * 8 * mcusY * c.VSamp * 8];
                c.Prediction = 0;
            }

            _bitBuffer = 0;
            _bitCount = 0;
            var mcuCount = 0;

            for (var my = 0; my < mcusY; my++)
            for (var mx = 0; mx < mcusX; mx++)
            {
                foreach (var (ci, dcId, acId) in scanComps)
                {
                    var comp = _components[ci];
                    for (var by = 0; by < comp.VSamp; by++)
                    for (var bx = 0; bx < comp.HSamp; bx++)
                    {
                        var block = DecodeBlock(comp, dcId, acId);
                        if (block is null)
                            return null;

                        var blockX = ((mx * comp.HSamp) + bx) * 8;
                        var blockY = ((my * comp.VSamp) + by) * 8;
                        var stride = comp.BlocksPerLine * 8;
                        PlaceBlock(block, comp.Pixels!, blockX, blockY, stride);
                    }
                }

                mcuCount++;
                if (_restartInterval > 0 && mcuCount % _restartInterval == 0 && !(my == mcusY - 1 && mx == mcusX - 1))
                    HandleRestart();
            }

            return Upsample(hMax, vMax);
        }

        private void HandleRestart()
        {
            _bitCount = 0;
            _bitBuffer = 0;
            // Skip to RSTn marker.
            while (_pos + 1 < _data.Length)
            {
                if (_data[_pos] == JpegConstants.MarkerPrefix &&
                    _data[_pos + 1] >= JpegConstants.RstFirst &&
                    _data[_pos + 1] <= JpegConstants.RstLast)
                {
                    _pos += 2;
                    break;
                }

                _pos++;
            }

            foreach (var c in _components) c.Prediction = 0;
        }

        private int[]? DecodeBlock(Component comp, int dcId, int acId)
        {
            var dcTable = _huff[0, dcId];
            var acTable = _huff[1, acId];
            var quant = _quant[comp.QuantId];

            if (dcTable is null || acTable is null || quant is null) return null;

            var coefficients = new int[64];

            var t = DecodeHuffman(dcTable);
            if (t < 0)
                return null;

            var diff = t == 0 ? 0 : Extend(ReceiveBits(t), t);
            comp.Prediction += diff;
            coefficients[0] = comp.Prediction * quant[0];

            var k = 1;
            while (k < 64)
            {
                var rs = DecodeHuffman(acTable);
                if (rs < 0)
                    return null;

                var r = rs >> 4;
                var s = rs & JpegConstants.NibbleMask;
                if (s == 0)
                {
                    if (r != 15)
                        break; // EOB

                    k += 16;
                    continue;
                }

                k += r;
                if (k >= 64)
                    break;

                var value = Extend(ReceiveBits(s), s);
                coefficients[JpegConstants.ZigZag[k]] = value * quant[JpegConstants.ZigZag[k]];
                k++;
            }

            return Idct(coefficients);
        }

        // ── Bit reading ──────────────────────────────────────────────────────────

        private int ReadBit()
        {
            if (_bitCount == 0)
            {
                if (_pos >= _data.Length)
                    return 0;

                var b = _data[_pos++];
                if (b == JpegConstants.MarkerPrefix)
                {
                    var next = _pos < _data.Length ? _data[_pos] : 0;

                    switch (next)
                    {
                        case JpegConstants.ByteStuff:
                            _pos++; // stuffed byte
                        break;
                        case >= JpegConstants.RstFirst and <= JpegConstants.RstLast:
                            /* restart, handled elsewhere */
                        break;
                    }
                    // else marker — leave for caller
                }

                _bitBuffer = b;
                _bitCount = 8;
            }

            _bitCount--;

            return (_bitBuffer >> _bitCount) & 1;
        }

        private int ReceiveBits(int count)
        {
            var v = 0;
            for (var i = 0; i < count; i++)
                v = (v << 1) | ReadBit();

            return v;
        }

        private static int Extend(int v, int t) =>
            v < 1 << (t - 1) ? v - (1 << t) + 1 : v;

        private int DecodeHuffman(HuffTable table)
        {
            // Canonical JPEG Huffman decode (ITU T.81 Annex F): grow the code one bit at a
            // time until it falls within the [MinCode, MaxCode] range for that length.
            var code = 0;
            for (var len = 1; len <= 16; len++)
            {
                code = (code << 1) | ReadBit();
                if (table.MaxCode[len] >= 0 && code <= table.MaxCode[len])
                    return table.Symbols[table.ValPtr[len] + (code - table.MinCode[len])];
            }

            return -1;
        }

        // ── IDCT (separable, float) ────────────────────────────────────────────────

        private static int[] Idct(IReadOnlyList<int> coefficients)
        {
            // Row-column separable IDCT.
            var output = new int[64];
            var block = new double[64];
            for (var i = 0; i < 64; i++) block[i] = coefficients[i];

            for (var row = 0; row < 8; row++)
                Idct1D(block, row * 8, 1);
            for (var col = 0; col < 8; col++)
                Idct1D(block, col, 8);

            for (var i = 0; i < 64; i++)
            {
                var v = (int)Math.Round((block[i] / 8.0) + 128);
                output[i] = v < 0 ? 0 : v > 255 ? 255 : v;
            }

            return output;
        }

        private static void Idct1D(IList<double> b, int offset, int stride)
        {
            // Naive 8-point IDCT — clear and correct; performance is acceptable for slides.
            Span<double> s = stackalloc double[8];
            for (var i = 0; i < 8; i++) s[i] = b[offset + (i * stride)];

            Span<double> o = stackalloc double[8];
            for (var x = 0; x < 8; x++)
            {
                double sum = 0;
                for (var u = 0; u < 8; u++)
                {
                    var cu = u == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                    sum += cu * s[u] * Math.Cos(((2.0 * x) + 1) * u * Math.PI / 16.0);
                }

                o[x] = sum;
            }

            for (var i = 0; i < 8; i++)
                b[offset + (i * stride)] = o[i];
        }

        private static void PlaceBlock(
            IReadOnlyList<int> block,
            IList<byte> dest,
            int x0,
            int y0,
            int stride
        )
        {
            for (var y = 0; y < 8; y++)
            {
                var dy = y0 + y;
                for (var x = 0; x < 8; x++)
                {
                    var dx = x0 + x;
                    var di = (dy * stride) + dx;
                    if (di >= 0 && di < dest.Count)
                        dest[di] = (byte)block[(y * 8) + x];
                }
            }
        }

        // ── Upsample + colour convert ────────────────────────────────────────────

        private byte[] Upsample(int hMax, int vMax)
        {
            var rgb = new byte[_width * _height * 3];

            if (_components.Length == 1)
            {
                var c = _components[0];
                var stride = c.BlocksPerLine * 8;
                for (var y = 0; y < _height; y++)
                for (var x = 0; x < _width; x++)
                {
                    var gray = c.Pixels![(y * stride) + x];
                    var d = ((y * _width) + x) * 3;
                    rgb[d] = rgb[d + 1] = rgb[d + 2] = gray;
                }

                return rgb;
            }

            var yc = _components[0];
            var cb = _components[1];
            var cr = _components[2];
            var yStride = yc.BlocksPerLine * 8;
            var cbStride = cb.BlocksPerLine * 8;
            var crStride = cr.BlocksPerLine * 8;

            for (var y = 0; y < _height; y++)
            for (var x = 0; x < _width; x++)
            {
                var yy = yc.Pixels![(y * yStride) + x];
                var cbx = x * cb.HSamp / hMax;
                var cby = y * cb.VSamp / vMax;
                var crx = x * cr.HSamp / hMax;
                var cry = y * cr.VSamp / vMax;
                var cbv = cb.Pixels![(cby * cbStride) + cbx] - 128;
                var crv = cr.Pixels![(cry * crStride) + crx] - 128;

                var r = yy + (YCbCrConstants.CrToR * crv);
                var g = yy - (YCbCrConstants.CbToGCb * cbv) - (YCbCrConstants.CrToGCr * crv);
                var b = yy + (YCbCrConstants.CbToB * cbv);

                var d = ((y * _width) + x) * 3;
                rgb[d] = Clamp(r);
                rgb[d + 1] = Clamp(g);
                rgb[d + 2] = Clamp(b);
            }

            return rgb;
        }

        private static byte Clamp(double v) => v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)Math.Round(v);

        private sealed class Component
        {
            public int BlocksPerLine;
            public int HSamp;
            public int Id;
            public byte[]? Pixels;
            public int Prediction;
            public int QuantId;
            public int VSamp;
        }

        private sealed class HuffTable
        {
            public readonly long[] MaxCode = new long[17];
            public readonly int[] MinCode = new int[17];
            public readonly byte[] Symbols;
            public readonly int[] ValPtr = new int[17];

            public HuffTable(IReadOnlyList<byte> counts, byte[] symbols)
            {
                Symbols = symbols;
                var code = 0;
                var k = 0;
                for (var len = 1; len <= 16; len++)
                {
                    if (counts[len - 1] == 0)
                        MaxCode[len] = -1;
                    else
                    {
                        ValPtr[len] = k;
                        MinCode[len] = code;
                        code += counts[len - 1];
                        k += counts[len - 1];
                        MaxCode[len] = code - 1;
                    }

                    // The code value MUST shift left for every length, including empty ones —
                    // otherwise all subsequent canonical codes are misaligned.
                    code <<= 1;
                }
            }
        }
    }
}
