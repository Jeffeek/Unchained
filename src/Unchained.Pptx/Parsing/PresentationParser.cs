using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Core;
using Unchained.Ooxml;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Security;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses a raw PPTX byte array (OPC package) into the Unchained presentation object model.
/// This is the top-level entry point for the M1–M4 parsing pipeline.
/// </summary>
internal sealed class PresentationParser
{
    /// <summary>
    /// Parses <paramref name="data"/> into a presentation and returns the key objects
    /// needed to construct the public <see cref="Engine.PresentationDocument"/> adapter.
    /// </summary>
    public static ParsedPresentation Parse(byte[] data, OpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        OpcPackage package;
        try
        {
            package = OpcPackage.Open(data);
        }
        catch (PptxEncryptedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PptxException("Failed to open the presentation package.", ex);
        }

        return ParsePackage(package, options);
    }

    /// <summary>
    /// Parses an already-opened <see cref="OpcPackage"/> into the presentation model.
    /// Takes ownership of <paramref name="package"/>.
    /// </summary>
    public static ParsedPresentation ParsePackage(OpcPackage package, OpenOptions? options = null)
    {
        // Locate the presentation part via package-level relationships
        var presentationRel = package.PackageRelationships
            .FirstOrDefault(r => r.RelationshipType.Equals(
                PmlNames.RelTypePresentation, StringComparison.Ordinal));

        if (presentationRel == null)
            throw new PptxException("The package does not contain a presentation relationship.");

        var presentationUri = "/" + presentationRel.TargetUri.TrimStart('/');
        var presentationPart = package.GetPart(presentationUri);
        var presentationXml = OoXmlHelper.ParseXml(presentationPart.Data);
        var root = presentationXml.Root
            ?? throw new PptxException("The presentation XML has no root element.");

        var mediaStore = new MediaStore();
        var masters = new MasterSlideCollection();
        var slides = new SlideCollection();

        // Parse slide size
        var slideSize = ParseSlideSize(root);

        // Parse properties
        var properties = ParseDocumentProperties(package);

        // Parse protection
        var protection = new ProtectionInfo { IsEncrypted = false };

        // Parse masters (and their themes + layouts)
        var masterParser = new MasterParser(package, mediaStore);
        foreach (var masterIdEl in root.Elements(PmlNames.SlideMasterIdList)
                                        .SelectMany(static l => l.Elements(PmlNames.SlideMasterId)))
        {
            var rId = (string?)masterIdEl.Attribute(PmlNames.RelationshipId);
            if (rId == null) continue;

            var masterRel = presentationPart.Relationships
                .FirstOrDefault(r =>
                    r.Id.Equals(rId, StringComparison.Ordinal));

            if (masterRel == null) continue;

            var masterUri = presentationPart.ResolveUri(masterRel.TargetUri);
            var master = masterParser.Parse(masterUri, rId);
            masters.Add(master);
        }

        // Parse slides
        var slideParser = new SlideParser(package, mediaStore, ToList(masters));
        foreach (var slideIdEl in root.Elements(PmlNames.SlideIdList)
                                       .SelectMany(static l => l.Elements(PmlNames.SlideId)))
        {
            var id = (uint)(slideIdEl.GetAttrInt(PmlNames.AttributeId) ?? 256);
            var rId = (string?)slideIdEl.Attribute(PmlNames.RelationshipId);
            if (rId == null) continue;

            var slideRel = presentationPart.Relationships
                .FirstOrDefault(r =>
                    r.Id.Equals(rId, StringComparison.Ordinal));

            if (slideRel == null) continue;

            var slideUri = presentationPart.ResolveUri(slideRel.TargetUri);
            var slide = slideParser.Parse(slideUri, rId, id);
            slides.AddParsed(slide);
        }

        // Update statistics
        properties.SlideCount = slides.Count;
        properties.HiddenSlideCount = slides.Count(static s => s.IsHidden);

        return new ParsedPresentation(
            package,
            slides,
            masters,
            mediaStore,
            properties,
            protection,
            slideSize);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SlideSize ParseSlideSize(System.Xml.Linq.XElement root)
    {
        var sldSz = root.Element(PmlNames.SlideSize);
        if (sldSz == null) return SlideSize.Widescreen;

        var cx = sldSz.GetAttrLong(PmlNames.AttributeWidth, SlideSize.Widescreen.Width.Value);
        var cy = sldSz.GetAttrLong(PmlNames.AttributeHeight, SlideSize.Widescreen.Height.Value);
        return new SlideSize(new Emu(cx), new Emu(cy));
    }

    private static DocumentProperties ParseDocumentProperties(OpcPackage package)
    {
        var props = new DocumentProperties();

        // Core properties
        var coreRel = package.PackageRelationships
            .FirstOrDefault(static r => r.RelationshipType.Equals(
                PmlNames.RelTypeCoreProperties, StringComparison.Ordinal));

        if (coreRel != null)
        {
            var corePart = package.TryGetPart("/" + coreRel.TargetUri.TrimStart('/'));
            if (corePart != null)
                ParseCoreProperties(corePart.Data, props);
        }

        return props;
    }

    private static void ParseCoreProperties(byte[] data, DocumentProperties props)
    {
        try
        {
            var doc = OoXmlHelper.ParseXml(data);
            var root = doc.Root;
            if (root == null) return;

            var dc = System.Xml.Linq.XNamespace.Get("http://purl.org/dc/elements/1.1/");
            var cp = System.Xml.Linq.XNamespace.Get(
                "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
            var dcterms = System.Xml.Linq.XNamespace.Get("http://purl.org/dc/terms/");

            props.Title = root.Element(dc + "title")?.Value;
            props.Subject = root.Element(dc + "subject")?.Value;
            props.Author = root.Element(dc + "creator")?.Value;
            props.Keywords = root.Element(cp + "keywords")?.Value;
            props.Description = root.Element(dc + "description")?.Value;
            props.LastModifiedBy = root.Element(cp + "lastModifiedBy")?.Value;
            props.Category = root.Element(cp + "category")?.Value;
            props.ContentStatus = root.Element(cp + "contentStatus")?.Value;

            var created = root.Element(dcterms + "created")?.Value;
            if (created != null && DateTimeOffset.TryParse(created, out var dt))
                props.Created = dt;

            var modified = root.Element(dcterms + "modified")?.Value;
            if (modified != null && DateTimeOffset.TryParse(modified, out var dtm))
                props.Modified = dtm;
        }
        catch
        {
            // Core properties are non-critical; swallow parse errors
        }
    }

    private static List<MasterSlide> ToList(MasterSlideCollection collection)
    {
        var list = new List<MasterSlide>(collection.Count);
        for (var i = 0; i < collection.Count; i++)
            list.Add(collection[i]);
        return list;
    }
}

/// <summary>
/// The result of parsing a PPTX package — all components needed to construct
/// the public presentation adapter.
/// </summary>
internal sealed class ParsedPresentation(
    OpcPackage package,
    SlideCollection slides,
    MasterSlideCollection masters,
    MediaStore mediaStore,
    Models.DocumentProperties properties,
    Security.ProtectionInfo protection,
    Core.SlideSize slideSize)
{
    public OpcPackage Package { get; } = package;
    public SlideCollection Slides { get; } = slides;
    public MasterSlideCollection Masters { get; } = masters;
    public MediaStore MediaStore { get; } = mediaStore;
    public Models.DocumentProperties Properties { get; } = properties;
    public Security.ProtectionInfo Protection { get; } = protection;
    public Core.SlideSize SlideSize { get; } = slideSize;
}
