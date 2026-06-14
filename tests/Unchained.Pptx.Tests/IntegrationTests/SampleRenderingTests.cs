using System.IO.Compression;
using System.Text;
using Shouldly;
using Unchained.Drawing.Constants;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Rendering.Models;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Regression guard against "blank slide" rendering. For each real python-pptx sample, this
///     derives from the model whether a slide actually carries renderable content (text, picture,
///     table cells, chart series, SmartArt nodes). Only then does it require non-trivial ink — so
///     genuinely-empty fixtures pass while real content that fails to rasterize fails the test.
///     The failure message names the content, making the test self-diagnosing.
/// </summary>
public sealed class SampleRenderingTests : PptxTestBase
{
    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "python-pptx", name);

    [Theory, InlineData("sld-slides.pptx"), InlineData("txt-font-props.pptx"), InlineData("shp-shapes.pptx"), InlineData("tbl-cell.pptx"),
     InlineData("shp-picture.pptx"), InlineData("cht-charts.pptx"), InlineData("shp-groupshape.pptx")]
    public async Task SlidesWithContent_RenderVisibleInk(string sample)
    {
        var path = SamplePath(sample);
        Assert.SkipUnless(File.Exists(path), $"sample missing: {sample}");

        var doc = await Processor.LoadAsync(await File.ReadAllBytesAsync(path));

        PptxImage[] images;
        try
        {
            images = await SlideRenderer.RenderAllAsync(doc, new RenderOptions { WidthPx = 1280, HeightPx = 720 });
        }
        catch (DllNotFoundException)
        {
            Assert.Skip("FreeType native not present (run scripts/FetchNatives/fetch-natives).");
            return;
        }

        for (var i = 0; i < doc.Slides.Count; i++)
        {
            var content = DescribeContent(doc.Slides[i]);
            var ink = InkPercent(images[i]);

            if (content is null)
                continue; // genuinely empty slide (e.g. an API fixture) — blank is correct

            ink.ShouldBeGreaterThan(
                0.1,
                $"{sample} slide {i + 1} has content [{content}] but rendered blank (ink={ink:0.00}%) " +
                "— it is missing from the rasterizer"
            );
        }
    }

    /// <summary>
    ///     Returns a short description of the renderable content on the slide, or <see langword="null" />
    ///     when the slide carries nothing that should produce ink.
    /// </summary>
    private static string? DescribeContent(Slide slide)
    {
        var parts = new List<string>();
        Walk(slide.Shapes, parts);
        return parts.Count == 0 ? null : string.Join(", ", parts);

        static void Walk(IEnumerable<Shape> shapes, ICollection<string> parts)
        {
            foreach (var s in shapes)
            {
                switch (s)
                {
                    case AutoShape a when !string.IsNullOrWhiteSpace(a.TextFrame.PlainText):
                        parts.Add($"text:\"{Trunc(a.TextFrame.PlainText)}\"{Geom(s)}");
                    break;
                    case PictureShape { Image: not null } p:
                        parts.Add($"picture({p.Image.ContentType}){Geom(s)}");
                    break;
                    case TableShape t when HasCellText(t):
                        parts.Add($"table[{t.Grid.ColumnCount}x{t.Grid.RowCount}]{Geom(s)}");
                    break;
                    case ChartShape c when c.Chart.Data.Series.Count > 0:
                        parts.Add($"chart{Geom(s)}");
                    break;
                    case SmartArtShape { Nodes.Count: > 0 }:
                        parts.Add($"smartart{Geom(s)}");
                    break;
                    case GroupShape g:
                        Walk(g.Children, parts);
                    break;
                }
            }
        }

        static bool HasCellText(TableShape t)
        {
            for (var r = 0; r < t.Grid.RowCount; r++)
            for (var c = 0; c < t.Grid.ColumnCount; c++)
            {
                if (!string.IsNullOrWhiteSpace(t.Grid[c, r].TextFrame.PlainText))
                    return true;
            }

            return false;
        }

        static string Trunc(string s) => s.Length <= 20 ? s : s[..20] + "...";
    }

    private static string Geom(Shape s) =>
        $"@{s.X.ToInches():0.0},{s.Y.ToInches():0.0} {s.Width.ToInches():0.0}x{s.Height.ToInches():0.0}in";

    /// <summary>Fraction of non-background pixels in a rendered PNG (0–100).</summary>
    private static double InkPercent(PptxImage image)
    {
        var rgb = DecodePng(image.Data.ToArray(), out var w, out var h);
        if (rgb is null) return 0;

        var counts = new Dictionary<int, int>();
        var pixels = w * h;
        for (var i = 0; i < pixels; i++)
        {
            var p = i * 3;
            var key = (rgb[p] << 16) | (rgb[p + 1] << 8) | rgb[p + 2];
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        var bg = counts.Count == 0 ? 0 : counts.Values.Max();
        return 100.0 * (pixels - bg) / Math.Max(1, pixels);
    }

    // ── Minimal PNG decoder (for ink measurement only) ──────────────────────────
    // Decodes the rasterizer's own PNG output: 8-bit truecolour/truecolour-alpha, no interlace.

    private static byte[]? DecodePng(byte[] png, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (png.Length < 8 || png[0] != 0x89 || png[1] != 0x50) return null;

        var pos = 8;
        var idat = new MemoryStream();
        var bitDepth = 0;
        var colorType = 0;

        while (pos + 8 <= png.Length)
        {
            var len = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
            var type = Encoding.ASCII.GetString(png, pos + 4, 4);
            var dataStart = pos + 8;
            if (type == PngConstants.IHDR)
            {
                width = (png[dataStart] << 24) | (png[dataStart + 1] << 16) | (png[dataStart + 2] << 8) | png[dataStart + 3];
                height = (png[dataStart + 4] << 24) | (png[dataStart + 5] << 16) | (png[dataStart + 6] << 8) | png[dataStart + 7];
                bitDepth = png[dataStart + 8];
                colorType = png[dataStart + 9];
            }
            else if (type == PngConstants.IDAT)
                idat.Write(png, dataStart, len);
            else if (type == PngConstants.IEND) break;

            pos = dataStart + len + 4; // skip data + CRC
        }

        if (width <= 0 || height <= 0 || bitDepth != 8) return null;

        var channels = colorType switch { 2 => 3, 6 => 4, _ => 0 };
        if (channels == 0) return null;

        idat.Position = 0;
        idat.ReadByte(); // skip 2-byte zlib header
        idat.ReadByte();
        using var deflate = new DeflateStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        deflate.CopyTo(raw);
        var data = raw.ToArray();

        var stride = width * channels;
        var rgb = new byte[width * height * 3];
        var prev = new byte[stride];
        var cur = new byte[stride];
        var srcPos = 0;
        for (var y = 0; y < height; y++)
        {
            if (srcPos >= data.Length) break;

            var filter = data[srcPos++];
            Array.Copy(data, srcPos, cur, 0, Math.Min(stride, data.Length - srcPos));
            srcPos += stride;
            Unfilter(filter, cur, prev, channels, stride);

            for (var x = 0; x < width; x++)
            {
                var s = x * channels;
                var d = ((y * width) + x) * 3;
                rgb[d] = cur[s];
                rgb[d + 1] = cur[s + 1];
                rgb[d + 2] = cur[s + 2];
            }

            (prev, cur) = (cur, prev);
        }

        return rgb;
    }

    private static void Unfilter(
        int filter,
        IList<byte> cur,
        IReadOnlyList<byte> prev,
        int bpp,
        int stride
    )
    {
        for (var i = 0; i < stride; i++)
        {
            var a = i >= bpp ? cur[i - bpp] : 0;
            int b = prev[i];
            var c = i >= bpp ? prev[i - bpp] : 0;
            int val = cur[i];
            cur[i] = filter switch
            {
                1 => (byte)(val + a),
                2 => (byte)(val + b),
                3 => (byte)(val + ((a + b) / 2)),
                4 => (byte)(val + Paeth(a, b, c)),
                _ => (byte)val
            };
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
