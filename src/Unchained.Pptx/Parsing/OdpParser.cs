using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Media;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Models.Themes;
using Unchained.Pptx.Security;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Themes;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses an OpenDocument Presentation (<c>.odp</c>) package into the Unchained presentation model.
///     Reads <c>content.xml</c> (<c>draw:page</c> → slide, <c>draw:frame</c> → shape, <c>text:p</c>/
///     <c>text:span</c> → paragraphs/runs) and the page geometry from <c>styles.xml</c>. This is a
///     structural import; advanced ODF styling is not mapped.
/// </summary>
internal static class OdpParser
{
    public const string MimeType = "application/vnd.oasis.opendocument.presentation";
    // ODF namespaces (kept local to the reader; the writer has its own copy in Export/OdfNames).
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace Presentation = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    /// <summary>Returns <see langword="true" /> when <paramref name="data" /> is an ODP package.</summary>
    public static bool IsOdp(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var mime = zip.GetEntry("mimetype");
            if (mime == null) return false;

            using var s = mime.Open();
            using var r = new StreamReader(s);
            return r.ReadToEnd().Trim().StartsWith(MimeType, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static ParsedPresentation Parse(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        var content = ReadXml(zip, "content.xml")
                      ?? throw new PptxException("ODP package has no content.xml.");
        var styles = ReadXml(zip, "styles.xml");

        var slideSize = ReadSlideSize(styles);
        var mediaStore = new MediaStore();

        // ODP has no concept of OOXML masters; synthesise one so slides have a valid layout.
        var master = new MasterSlide { Name = "Default", Theme = new PptxTheme() };
        var layout = new SlideLayout { Name = "Default", LayoutType = LayoutType.Blank, Master = master };
        master.Layouts.Add(layout);
        var masters = new MasterSlideCollection { master };

        var slides = new SlideCollection();

        var body = content.Root?.Element(Office + "body")?.Element(Office + "presentation");
        uint slideId = 256;
        foreach (var page in body?.Elements(Draw + "page") ?? [])
        {
            var slide = new Slide
            {
                SlideId = slideId++,
                Layout = layout,
                Name = (string?)page.Attribute(Draw + "name") ?? string.Empty,
                IsHidden = (string?)page.Attribute(Presentation + "visibility") == "hidden"
            };

            foreach (var frame in page.Elements(Draw + "frame"))
                ReadFrame(frame, slide, mediaStore, zip);

            slides.AddParsed(slide);
        }

        var properties = ReadProperties(zip);
        properties.SlideCount = slides.Count;
        properties.HiddenSlideCount = slides.Count(static s => s.IsHidden);

        return new ParsedPresentation(
            null,
            slides,
            masters,
            mediaStore,
            properties,
            new ProtectionInfo(),
            slideSize,
            new CommentAuthorCollection(),
            new SectionCollection());
    }

    // ── Frame → shape ────────────────────────────────────────────────────────

    private static void ReadFrame(
        XElement frame,
        Slide slide,
        MediaStore mediaStore,
        ZipArchive zip
    )
    {
        var x = ParseLength((string?)frame.Attribute(Svg + "x"));
        var y = ParseLength((string?)frame.Attribute(Svg + "y"));
        var w = ParseLength((string?)frame.Attribute(Svg + "width"));
        var h = ParseLength((string?)frame.Attribute(Svg + "height"));

        var image = frame.Element(Draw + "image");
        if (image != null)
        {
            var href = (string?)image.Attribute(XLink + "href");
            var img = LoadImage(href, zip, mediaStore);
            if (img != null)
            {
                var pic = new PictureShape { X = x, Y = y, Width = w, Height = h, Image = img };
                slide.Shapes.AddParsed(pic);
                return;
            }
        }

        var textBox = frame.Element(Draw + "text-box");
        if (textBox == null) return;

        var shape = new AutoShape
        {
            ShapeType = AutoShapeType.Rectangle,
            IsTextBox = true,
            X = x, Y = y, Width = w, Height = h
        };
        ReadTextBox(textBox, shape);
        slide.Shapes.AddParsed(shape);
    }

    private static void ReadTextBox(XContainer textBox, AutoShape shape)
    {
        foreach (var pEl in textBox.Elements(TextNs + "p"))
        {
            var para = shape.TextFrame.Paragraphs.Add();
            var hasRun = false;
            foreach (var node in pEl.Nodes())
            {
                switch (node)
                {
                    case XElement span when span.Name == TextNs + "span":
                        para.Runs.Add(span.Value);
                        hasRun = true;
                    break;
                    case XText text:
                        para.Runs.Add(text.Value);
                        hasRun = true;
                    break;
                }
            }

            // A paragraph with text directly in <text:p> (no span) — capture it.
            if (!hasRun && !string.IsNullOrEmpty(pEl.Value))
                para.Runs.Add(pEl.Value);
        }
    }

    private static EmbeddedImage? LoadImage(string? href, ZipArchive zip, MediaStore mediaStore)
    {
        if (string.IsNullOrEmpty(href)) return null;

        var entryName = href.TrimStart('/');
        var entry = zip.GetEntry(entryName);
        if (entry == null) return null;

        using var s = entry.Open();
        using var outMs = new MemoryStream();
        s.CopyTo(outMs);
        var bytes = outMs.ToArray();

        var contentType = ExtensionToMime(Path.GetExtension(entryName));
        var image = new EmbeddedImage(contentType, bytes) { PartUri = "/" + entryName };
        return mediaStore.AddImage(image);
    }

    // ── Geometry & metadata ────────────────────────────────────────────────────

    private static SlideSize ReadSlideSize(XDocument? styles)
    {
        var props = styles?.Root?
            .Element(Office + "automatic-styles")?
            .Elements(Style + "page-layout")
            .FirstOrDefault()?
            .Element(Style + "page-layout-properties");

        var w = ParseLength((string?)props?.Attribute(Fo + "page-width"));
        var h = ParseLength((string?)props?.Attribute(Fo + "page-height"));

        return w.Value > 0 && h.Value > 0 ? new SlideSize(w, h) : SlideSize.Widescreen;
    }

    private static DocumentProperties ReadProperties(ZipArchive zip)
    {
        var props = new DocumentProperties();
        var meta = ReadXml(zip, "meta.xml");
        var metaEl = meta?.Root?.Element(Office + "meta");
        if (metaEl == null) return props;

        var dc = XNamespace.Get("http://purl.org/dc/elements/1.1/");
        var metaNs = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:meta:1.0");
        props.Title = (string?)metaEl.Element(dc + "title");
        props.Author = (string?)metaEl.Element(metaNs + "initial-creator");
        return props;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static XDocument? ReadXml(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        if (entry == null) return null;

        using var s = entry.Open();
        return XDocument.Load(s);
    }

    /// <summary>Parses an ODF length (e.g. "12.7cm", "360pt", "5in") into EMU.</summary>
    private static Emu ParseLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Emu.Zero;

        var unit = value.Length >= 2 ? value[^2..] : string.Empty;
        var numberText = value[..^unit.Length];
        return !double.TryParse(numberText,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number)
            ? Emu.Zero
            : unit switch
            {
                "cm" => Emu.FromCentimetres(number),
                "mm" => Emu.FromCentimetres(number / 10.0),
                "in" => Emu.FromInches(number),
                "pt" => Emu.FromPoints(number),
                _ => Emu.FromCentimetres(number)
            };
    }

    private static string ExtensionToMime(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        _ => "application/octet-stream"
    };
}
