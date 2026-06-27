using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;

namespace Unchained.Pptx.Export;

/// <summary>
///     Exports a <see cref="PresentationDocument" /> to OpenDocument Presentation (<c>.odp</c>) format.
///     Produces a valid ODF package: an uncompressed <c>mimetype</c> entry followed by
///     <c>content.xml</c>, <c>styles.xml</c>, <c>meta.xml</c>, and <c>META-INF/manifest.xml</c>.
///     Slides map to <c>draw:page</c>, shapes to <c>draw:frame</c>, and text to <c>text:p</c>/<c>text:span</c>.
///     This is a one-directional structural export; advanced effects are not translated.
/// </summary>
internal static class PptxToOdpWriter
{
    public static byte[] Write(PresentationDocument document, OdpSaveOptions options)
    {
        var images = new List<(string Path, byte[] Data, string Mime)>();
        var content = BuildContent(document, options, images);
        var styles = BuildStyles(document);
        var meta = BuildMeta(document);
        var manifest = BuildManifest(images);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            // The mimetype entry MUST be first and stored (uncompressed) per the ODF spec.
            WriteStored(zip, "mimetype", Encoding.ASCII.GetBytes(OdfNames.PresentationMimeType));

            WriteDeflated(zip, "content.xml", content);
            WriteDeflated(zip, "styles.xml", styles);
            WriteDeflated(zip, "meta.xml", meta);
            WriteDeflated(zip, "META-INF/manifest.xml", manifest);

            foreach (var (path, data, _) in images)
                WriteDeflated(zip, path, data);
        }

        options.Progress?.Report(1.0);
        return ms.ToArray();
    }

    // ── content.xml ───────────────────────────────────────────────────────────

    private static byte[] BuildContent(
        PresentationDocument document,
        OdpSaveOptions options,
        ICollection<(string Path, byte[] Data, string Mime)> images
    )
    {
        var o = OdfNames.Office;
        var draw = OdfNames.Draw;
        var pres = OdfNames.Presentation;

        var body = new XElement(o + "presentation");
        var slides = document.Slides;

        var included = Enumerable.Range(0, slides.Count)
            .Where(i => !slides[i].IsHidden || options.IncludeHiddenSlides)
            .ToList();

        for (var idx = 0; idx < included.Count; idx++)
        {
            options.Progress?.Report(0.5 * idx / Math.Max(1, included.Count));
            var slide = slides[included[idx]];
            var page = new XElement(
                draw + "page",
                new XAttribute(draw + "name", slide.Name.Length > 0 ? slide.Name : $"page{idx + 1}"),
                new XAttribute(draw + "master-page-name", "Default")
            );

            if (slide.IsHidden)
                page.Add(new XAttribute(pres + "visibility", "hidden"));

            foreach (var shape in slide.Shapes)
                WriteShape(page, shape, images, options);

            body.Add(page);
        }

        var root = new XElement(
            o + "document-content",
            NamespaceDeclarations(),
            new XAttribute(o + "version", "1.2"),
            new XElement(o + "automatic-styles"),
            new XElement(o + "body", body)
        );

        return Serialize(root);
    }

    private static void WriteShape(
        XContainer page,
        Shape shape,
        ICollection<(string Path, byte[] Data, string Mime)> images,
        OdpSaveOptions options
    )
    {
        var draw = OdfNames.Draw;

        // Position/size as ODF length attributes (centimetres).
        var x = $"{shape.X.ToCentimetres():F3}cm";
        var y = $"{shape.Y.ToCentimetres():F3}cm";
        var w = $"{shape.Width.ToCentimetres():F3}cm";
        var h = $"{shape.Height.ToCentimetres():F3}cm";

        switch (shape)
        {
            case PictureShape { Image: not null } pic when options.EmbedImages:
            {
                var ext = ImageExtensions.Extension(pic.Image.ContentType);
                var path = $"Pictures/image{images.Count + 1}{ext}";
                images.Add((path, pic.Image.Data.ToArray(), pic.Image.ContentType));

                var frame = new XElement(
                    draw + "frame",
                    new XAttribute(OdfNames.Svg + "x", x),
                    new XAttribute(OdfNames.Svg + "y", y),
                    new XAttribute(OdfNames.Svg + "width", w),
                    new XAttribute(OdfNames.Svg + "height", h),
                    new XElement(
                        draw + "image",
                        new XAttribute(OdfNames.XLink + "href", path),
                        new XAttribute(OdfNames.XLink + "type", "simple")
                    )
                );
                page.Add(frame);
                break;
            }

            case AutoShape auto:
            {
                var frame = new XElement(
                    draw + "frame",
                    new XAttribute(OdfNames.Svg + "x", x),
                    new XAttribute(OdfNames.Svg + "y", y),
                    new XAttribute(OdfNames.Svg + "width", w),
                    new XAttribute(OdfNames.Svg + "height", h)
                );

                var textBox = new XElement(draw + "text-box");
                WriteText(textBox, auto.TextFrame);
                frame.Add(textBox);
                page.Add(frame);
                break;
            }

            case GroupShape group:
                foreach (var child in group.Children)
                    WriteShape(page, child, images, options);
            break;
        }
    }

    private static void WriteText(XContainer container, TextFrame frame)
    {
        var t = OdfNames.Text;
        foreach (var para in frame.Paragraphs)
        {
            var p = new XElement(t + "p");
            foreach (var run in para.Runs.Where(static run => !string.IsNullOrEmpty(run.Text)))
                // Plain span; ODF run formatting requires automatic styles, out of scope here.
                p.Add(new XElement(t + "span", run.Text));

            container.Add(p);
        }
    }

    // ── styles.xml / meta.xml / manifest.xml ────────────────────────────────────

    private static byte[] BuildStyles(PresentationDocument document)
    {
        var o = OdfNames.Office;
        var style = OdfNames.Style;

        var widthCm = document.SlideSize.Width.ToCentimetres();
        var heightCm = document.SlideSize.Height.ToCentimetres();

        // A single page-layout + master-page named "Default" so content.xml's references resolve.
        var pageLayout = new XElement(
            style + "page-layout",
            new XAttribute(style + "name", "PL"),
            new XElement(
                style + "page-layout-properties",
                new XAttribute(OdfNames.Fo + "page-width", $"{widthCm:F3}cm"),
                new XAttribute(OdfNames.Fo + "page-height", $"{heightCm:F3}cm"),
                new XAttribute(
                    style + "print-orientation",
                    widthCm >= heightCm ? "landscape" : "portrait"
                )
            )
        );

        var masterPage = new XElement(
            style + "master-page",
            new XAttribute(style + "name", "Default"),
            new XAttribute(style + "page-layout-name", "PL")
        );

        var root = new XElement(
            o + "document-styles",
            NamespaceDeclarations(),
            new XAttribute(o + "version", "1.2"),
            new XElement(o + "styles"),
            new XElement(o + "automatic-styles", pageLayout),
            new XElement(o + "master-styles", masterPage)
        );

        return Serialize(root);
    }

    private static byte[] BuildMeta(PresentationDocument document)
    {
        var o = OdfNames.Office;
        var meta = new XElement(
            o + "meta",
            new XElement(OdfNames.Meta + "generator", "Unchained.Pptx")
        );

        var title = document.Properties.Title;
        if (!string.IsNullOrEmpty(title))
            meta.Add(new XElement(OdfNames.Dc + "title", title));
        var author = document.Properties.Author;
        if (!string.IsNullOrEmpty(author))
            meta.Add(new XElement(OdfNames.Meta + "initial-creator", author));

        var root = new XElement(
            o + "document-meta",
            NamespaceDeclarations(),
            new XAttribute(o + "version", "1.2"),
            meta
        );

        return Serialize(root);
    }

    private static byte[] BuildManifest(List<(string Path, byte[] Data, string Mime)> images)
    {
        var m = OdfNames.Manifest;

        var root = new XElement(
            m + "manifest",
            new XAttribute(XNamespace.Xmlns + "manifest", m.NamespaceName),
            new XAttribute(m + "version", "1.2")
        );

        Entry("/", OdfNames.PresentationMimeType);
        Entry("content.xml", "text/xml");
        Entry("styles.xml", "text/xml");
        Entry("meta.xml", "text/xml");
        foreach (var (path, _, mime) in images)
            Entry(path, mime);

        return Serialize(root);

        void Entry(string path, string mime) =>
            root.Add(
                new XElement(
                    m + "file-entry",
                    new XAttribute(m + "full-path", path),
                    new XAttribute(m + "media-type", mime)
                )
            );
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static XAttribute[] NamespaceDeclarations() =>
    [
        new(XNamespace.Xmlns + "office", OdfNames.Office.NamespaceName),
        new(XNamespace.Xmlns + "draw", OdfNames.Draw.NamespaceName),
        new(XNamespace.Xmlns + "text", OdfNames.Text.NamespaceName),
        new(XNamespace.Xmlns + "style", OdfNames.Style.NamespaceName),
        new(XNamespace.Xmlns + "fo", OdfNames.Fo.NamespaceName),
        new(XNamespace.Xmlns + "svg", OdfNames.Svg.NamespaceName),
        new(XNamespace.Xmlns + "presentation", OdfNames.Presentation.NamespaceName),
        new(XNamespace.Xmlns + "meta", OdfNames.Meta.NamespaceName),
        new(XNamespace.Xmlns + "xlink", OdfNames.XLink.NamespaceName),
        new(XNamespace.Xmlns + "dc", OdfNames.Dc.NamespaceName)
    ];

    private static byte[] Serialize(XElement root)
    {
        using var ms = new MemoryStream();
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).Save(ms);
        return ms.ToArray();
    }

    private static void WriteStored(ZipArchive zip, string name, byte[] data)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
        using var s = entry.Open();
        s.Write(data, 0, data.Length);
    }

    private static void WriteDeflated(ZipArchive zip, string name, byte[] data)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(data, 0, data.Length);
    }
}
