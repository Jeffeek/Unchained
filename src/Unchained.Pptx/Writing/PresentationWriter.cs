using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Core;
using System.Text;
using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Security;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Writing;

/// <summary>
/// Serializes the Unchained presentation object model back into a valid PPTX
/// (OPC/ZIP) package. This is the top-level entry point for the M1–M4 write path.
/// </summary>
internal sealed class PresentationWriter
{
    // ── Part URI templates ────────────────────────────────────────────────────

    private const string PresentationPartUri = "/ppt/presentation.xml";
    private const string CorePropsPartUri = "/docProps/core.xml";

    /// <summary>
    /// Serializes <paramref name="slides"/>, <paramref name="masters"/>,
    /// and supporting data into a new <see cref="OpcPackage"/> and returns the
    /// raw PPTX bytes.
    /// </summary>
    public static byte[] Write(
        SlideCollection slides,
        MasterSlideCollection masters,
        MediaStore mediaStore,
        DocumentProperties properties,
        SlideSize slideSize,
        Models.SaveOptions? options = null)
    {
        var package = OpcPackage.CreateEmpty();

        // Register standard defaults
        var contentTypes = new ContentTypeMap();
        contentTypes.RegisterDefault("rels",
            "application/vnd.openxmlformats-package.relationships+xml");
        contentTypes.RegisterDefault("xml", "application/xml");

        // Assign master relationship IDs before writing presentation.xml
        for (var i = 0; i < masters.Count; i++)
        {
            var master = masters[i];
            if (string.IsNullOrEmpty(master.RelationshipId))
                master.RelationshipId = $"rId{100 + i}";
        }

        for (var i = 0; i < slides.Count; i++)
        {
            var slide = slides[i];
            if (string.IsNullOrEmpty(slide.RelationshipId))
                slide.RelationshipId = $"rId{i + 1}";
        }

        // Write media assets first so shapes can reference relationship IDs
        var imageRelIds = WriteMedia(package, mediaStore);

        // Write masters + their themes + layouts
        var masterUris = WriteMasters(package, masters, contentTypes, imageRelIds);

        // Write slides
        var slideUris = WriteSlides(package, slides, contentTypes, imageRelIds);

        // Write presentation.xml
        WritePresentationPart(package, slides, masters, masterUris, slideUris, slideSize, contentTypes);

        // Write core properties
        WriteCoreProperties(package, properties, contentTypes);

        // Package-level relationships
        package.AddPackageRelationship("rId1", PmlNames.RelTypePresentation,
            PresentationPartUri.TrimStart('/'));
        package.AddPackageRelationship("rId2", PmlNames.RelTypeCoreProperties,
            CorePropsPartUri.TrimStart('/'));

        return package.Save();
    }

    // ── Media ─────────────────────────────────────────────────────────────────

    private static Dictionary<string, string> WriteMedia(
        OpcPackage package,
        MediaStore mediaStore)
    {
        var relIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 1;

        foreach (var image in mediaStore.Images)
        {
            var extension = ExtensionForContentType(image.ContentType);
            var uri = $"/ppt/media/image{index++}{extension}";
            image.PartUri = uri;
            package.AddOrReplacePart(uri, image.ContentType, image.Data.ToArray());
        }

        return relIdMap;
    }

    // ── Masters ───────────────────────────────────────────────────────────────

    private static Dictionary<MasterSlide, string> WriteMasters(
        OpcPackage package,
        MasterSlideCollection masters,
        ContentTypeMap contentTypes,
        Dictionary<string, string> imageRelIds)
    {
        var masterUris = new Dictionary<MasterSlide, string>();

        for (var i = 0; i < masters.Count; i++)
        {
            var master = masters[i];
            var masterUri = string.IsNullOrEmpty(master.PartUri)
                ? $"/ppt/slideMasters/slideMaster{i + 1}.xml"
                : master.PartUri;

            master.PartUri = masterUri;
            contentTypes.Register(masterUri, PmlNames.ContentTypeSlideMaster);

            // Write theme
            var themeUri = $"/ppt/theme/theme{i + 1}.xml";
            var themeXml = ThemeWriter.Write(master.Theme);
            package.AddOrReplacePart(themeUri, PmlNames.ContentTypeTheme,
                new XDocument(themeXml).ToUtf8Bytes());
            contentTypes.Register(themeUri, PmlNames.ContentTypeTheme);

            // Write layouts
            var layoutUris = WriteLayouts(package, master.Layouts, i + 1, contentTypes);

            // Write master XML
            var masterXml = MasterWriter.Write(master, themeUri, layoutUris);
            package.AddOrReplacePart(masterUri, PmlNames.ContentTypeSlideMaster,
                new XDocument(masterXml).ToUtf8Bytes());

            // Master relationships
            var rId = 1;
            package.AddRelationship(masterUri,
                $"rId{rId++}", PmlNames.RelTypeTheme, themeUri);
            foreach (var (layout, layoutUri) in layoutUris)
            {
                package.AddRelationship(masterUri,
                    layout.RelationshipId.Length > 0 ? layout.RelationshipId : $"rId{rId++}",
                    PmlNames.RelTypeSlideLayout,
                    RelativeUri(masterUri, layoutUri));
            }

            masterUris[master] = masterUri;
        }

        return masterUris;
    }

    private static Dictionary<SlideLayout, string> WriteLayouts(
        OpcPackage package,
        SlideLayoutCollection layouts,
        int masterIndex,
        ContentTypeMap contentTypes)
    {
        var layoutUris = new Dictionary<SlideLayout, string>();
        var rIdCounter = 1;

        for (var j = 0; j < layouts.Count; j++)
        {
            var layout = layouts[j];
            var layoutUri = string.IsNullOrEmpty(layout.PartUri)
                ? $"/ppt/slideLayouts/slideLayout{(masterIndex - 1) * 20 + j + 1}.xml"
                : layout.PartUri;

            layout.PartUri = layoutUri;
            if (string.IsNullOrEmpty(layout.RelationshipId))
                layout.RelationshipId = $"rId{rIdCounter++}";

            contentTypes.Register(layoutUri, PmlNames.ContentTypeSlideLayout);

            var layoutXml = LayoutWriter.Write(layout);
            package.AddOrReplacePart(layoutUri, PmlNames.ContentTypeSlideLayout,
                new XDocument(layoutXml).ToUtf8Bytes());

            // Layout → master relationship
            package.AddRelationship(layoutUri, "rId1", PmlNames.RelTypeSlideMaster,
                RelativeUri(layoutUri, layout.Master.PartUri));

            layoutUris[layout] = layoutUri;
        }

        return layoutUris;
    }

    // ── Slides ────────────────────────────────────────────────────────────────

    private static Dictionary<Slide, string> WriteSlides(
        OpcPackage package,
        SlideCollection slides,
        ContentTypeMap contentTypes,
        Dictionary<string, string> imageRelIds)
    {
        var slideUris = new Dictionary<Slide, string>();

        for (var i = 0; i < slides.Count; i++)
        {
            var slide = slides[i];
            var slideUri = string.IsNullOrEmpty(slide.PartUri)
                ? $"/ppt/slides/slide{i + 1}.xml"
                : slide.PartUri;

            slide.PartUri = slideUri;
            contentTypes.Register(slideUri, PmlNames.ContentTypeSlide);

            var slideXml = SlideWriter.Write(slide);
            package.AddOrReplacePart(slideUri, PmlNames.ContentTypeSlide,
                new XDocument(slideXml).ToUtf8Bytes());

            // Slide relationships
            var rId = 1;

            // Layout relationship
            package.AddRelationship(slideUri, $"rId{rId++}",
                PmlNames.RelTypeSlideLayout,
                RelativeUri(slideUri, slide.Layout.PartUri));

            // Image relationships
            foreach (var shape in slide.Shapes.OfType<Shapes.PictureShape>()
                .Where(static p => p.Image != null && p.Image.PartUri.Length > 0))
            {
                if (string.IsNullOrEmpty(shape.Image!.RelationshipId))
                    shape.Image.RelationshipId = $"rId{rId++}";

                package.AddRelationship(slideUri,
                    shape.Image.RelationshipId,
                    PmlNames.RelTypeImage,
                    RelativeUri(slideUri, shape.Image.PartUri));
            }

            slideUris[slide] = slideUri;
        }

        return slideUris;
    }

    // ── Presentation part ─────────────────────────────────────────────────────

    private static void WritePresentationPart(
        OpcPackage package,
        SlideCollection slides,
        MasterSlideCollection masters,
        Dictionary<MasterSlide, string> masterUris,
        Dictionary<Slide, string> slideUris,
        SlideSize slideSize,
        ContentTypeMap contentTypes)
    {
        var pml = PmlNames.Pml;
        var r = PmlNames.Relationships;

        var pres = new XElement(PmlNames.Presentation,
            new XAttribute(XNamespace.Xmlns + "p", pml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName));

        // Slide master IDs
        var masterIdLst = new XElement(PmlNames.SlideMasterIdList);
        for (var i = 0; i < masters.Count; i++)
        {
            var master = masters[i];
            masterIdLst.Add(new XElement(PmlNames.SlideMasterId,
                new XAttribute(PmlNames.AttributeId, (uint)(2_147_483_648 + i)),
                new XAttribute(PmlNames.RelationshipId, master.RelationshipId.Length > 0
                    ? master.RelationshipId
                    : $"rId{100 + i}")));
        }
        pres.Add(masterIdLst);

        // Slide size
        pres.Add(new XElement(PmlNames.SlideSize,
            new XAttribute(PmlNames.AttributeWidth, slideSize.Width.Value),
            new XAttribute(PmlNames.AttributeHeight, slideSize.Height.Value)));

        pres.Add(new XElement(PmlNames.NotesSize,
            new XAttribute(PmlNames.AttributeWidth, Emu.FromInches(7.5).Value),
            new XAttribute(PmlNames.AttributeHeight, Emu.FromInches(10).Value)));

        // Slide IDs
        var slideIdLst = new XElement(PmlNames.SlideIdList);
        for (var i = 0; i < slides.Count; i++)
        {
            var slide = slides[i];
            slideIdLst.Add(new XElement(PmlNames.SlideId,
                new XAttribute(PmlNames.AttributeId, slide.SlideId),
                new XAttribute(PmlNames.RelationshipId,
                    slide.RelationshipId.Length > 0 ? slide.RelationshipId : $"rId{i + 1}")));
        }
        pres.Add(slideIdLst);

        var presXml = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            pres);

        package.AddOrReplacePart(PresentationPartUri,
            PmlNames.ContentTypePresentation,
            presXml.ToUtf8Bytes());
        contentTypes.Register(PresentationPartUri, PmlNames.ContentTypePresentation);

        // Presentation relationships
        var rIdCounter = 1;
        foreach (var master in Enumerable.Range(0, masters.Count).Select(i => masters[i]))
        {
            var rId = master.RelationshipId.Length > 0
                ? master.RelationshipId
                : $"rId{100 + rIdCounter}";
            master.RelationshipId = rId;
            package.AddRelationship(PresentationPartUri, rId,
                PmlNames.RelTypeSlideMaster,
                RelativeUri(PresentationPartUri, master.PartUri));
            rIdCounter++;
        }

        for (var i = 0; i < slides.Count; i++)
        {
            var slide = slides[i];
            var rId = slide.RelationshipId.Length > 0
                ? slide.RelationshipId
                : $"rId{i + 1}";
            slide.RelationshipId = rId;
            package.AddRelationship(PresentationPartUri, rId,
                PmlNames.RelTypeSlide,
                RelativeUri(PresentationPartUri, slide.PartUri));
        }
    }

    // ── Core properties ───────────────────────────────────────────────────────

    private static void WriteCoreProperties(
        OpcPackage package,
        DocumentProperties properties,
        ContentTypeMap contentTypes)
    {
        var cp = XNamespace.Get(
            "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
        var dc = XNamespace.Get("http://purl.org/dc/elements/1.1/");
        var dcterms = XNamespace.Get("http://purl.org/dc/terms/");
        var xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

        var root = new XElement(cp + "coreProperties",
            new XAttribute(XNamespace.Xmlns + "cp", cp.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dcterms", dcterms.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName));

        if (properties.Title != null)
            root.Add(new XElement(dc + "title", properties.Title));
        if (properties.Subject != null)
            root.Add(new XElement(dc + "subject", properties.Subject));
        if (properties.Author != null)
            root.Add(new XElement(dc + "creator", properties.Author));
        if (properties.Keywords != null)
            root.Add(new XElement(cp + "keywords", properties.Keywords));
        if (properties.Description != null)
            root.Add(new XElement(dc + "description", properties.Description));
        if (properties.LastModifiedBy != null)
            root.Add(new XElement(cp + "lastModifiedBy", properties.LastModifiedBy));
        if (properties.Category != null)
            root.Add(new XElement(cp + "category", properties.Category));
        if (properties.ContentStatus != null)
            root.Add(new XElement(cp + "contentStatus", properties.ContentStatus));

        var now = DateTimeOffset.UtcNow;
        root.Add(new XElement(dcterms + "created",
            new XAttribute(xsi + "type", "dcterms:W3CDTF"),
            (properties.Created ?? now).ToString("yyyy-MM-ddTHH:mm:ssZ")));
        root.Add(new XElement(dcterms + "modified",
            new XAttribute(xsi + "type", "dcterms:W3CDTF"),
            (properties.Modified ?? now).ToString("yyyy-MM-ddTHH:mm:ssZ")));

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        package.AddOrReplacePart(CorePropsPartUri,
            PmlNames.ContentTypeCoreProperties,
            doc.ToUtf8Bytes());
        contentTypes.Register(CorePropsPartUri, PmlNames.ContentTypeCoreProperties);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a relative URI from <paramref name="sourceUri"/> to <paramref name="targetUri"/>.
    /// Both must be absolute OPC part URIs starting with '/'.
    /// </summary>
    private static string RelativeUri(string sourceUri, string targetUri)
    {
        var sourceDir = System.IO.Path.GetDirectoryName(sourceUri)?.Replace('\\', '/') ?? "/";
        if (!sourceDir.EndsWith('/')) sourceDir += '/';

        if (targetUri.StartsWith(sourceDir, StringComparison.OrdinalIgnoreCase))
            return targetUri[sourceDir.Length..];

        // Walk up
        var sourceParts = sourceDir.TrimStart('/').Split('/');
        var targetParts = targetUri.TrimStart('/').Split('/');
        var common = 0;

        while (common < sourceParts.Length &&
               common < targetParts.Length &&
               sourceParts[common].Equals(targetParts[common], StringComparison.OrdinalIgnoreCase))
        {
            common++;
        }

        var up = string.Concat(Enumerable.Repeat("../", sourceParts.Length - common));
        var down = string.Join("/", targetParts.Skip(common));
        return up + down;
    }

    private static string ExtensionForContentType(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" or "image/jpg" => ".jpeg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "image/tiff" => ".tiff",
        "image/svg+xml" => ".svg",
        "image/x-emf" or "image/emf" => ".emf",
        "image/x-wmf" or "image/wmf" => ".wmf",
        _ => ".bin"
    };
}
