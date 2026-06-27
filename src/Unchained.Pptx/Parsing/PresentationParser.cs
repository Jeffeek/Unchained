using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Engine;
using Unchained.Ooxml.Media;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Properties;
using Unchained.Ooxml.Security;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Security;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses a raw PPTX byte array (OPC package) into the Unchained presentation object model.
///     This is the top-level entry point for the M1–M4 parsing pipeline.
/// </summary>
internal sealed class PresentationParser
{
    /// <summary>
    ///     Parses <paramref name="data" /> into a presentation and returns the key objects
    ///     needed to construct the public <see cref="Engine.PresentationDocument" /> adapter.
    /// </summary>
    public static ParsedPresentation Parse(byte[] data, OpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Detect OLE CFB (encrypted OOXML) and decrypt if needed (M8)
        var wasEncrypted = false;
        if (AgileEncryption.IsCfb(data))
        {
            wasEncrypted = true;
            if (string.IsNullOrEmpty(options?.Password))
                throw new PptxEncryptedException();

            try
            {
                data = AgileEncryption.Decrypt(data, options.Password);
            }
            catch (OoXmlEncryptedException ex)
            {
                throw new PptxEncryptedException(ex.Message, ex);
            }
        }

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

        var result = ParsePackage(package);
        if (wasEncrypted) result.Protection.IsEncrypted = true;
        return result;
    }

    /// <summary>
    ///     Parses an already-opened <see cref="OpcPackage" /> into the presentation model.
    ///     Takes ownership of <paramref name="package" />.
    /// </summary>
    public static ParsedPresentation ParsePackage(OpcPackage package)
    {
        // Locate the presentation part via package-level relationships
        var presentationRel = package.PackageRelationships
            .FirstOrDefault(static r => r.RelationshipType.Equals(
                    PmlNames.RelTypePresentation,
                    StringComparison.Ordinal
                )
            );

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
        var commentAuthors = new CommentAuthorCollection();
        var sections = new SectionCollection();

        // Parse slide size
        var slideSize = ParseSlideSize(root);

        // Parse properties
        var properties = ParseDocumentProperties(package);

        // Parse protection (M8)
        var protection = ParseProtection(root);

        // Parse masters (and their themes + layouts)
        var masterParser = new MasterParser(package);
        // ReSharper disable once LoopCanBePartlyConvertedToQuery
        foreach (var masterIdEl in root.Elements(PmlNames.SlideMasterIdList)
                     .SelectMany(static l => l.Elements(PmlNames.SlideMasterId)))
        {
            var rId = (string?)masterIdEl.Attribute(PmlNames.RelationshipId);
            if (rId == null) continue;

            var masterRel = presentationPart.Relationships
                .FirstOrDefault(r =>
                    r.Id.Equals(rId, StringComparison.Ordinal)
                );

            if (masterRel == null) continue;

            var masterUri = presentationPart.ResolveUri(masterRel.TargetUri);
            var master = masterParser.Parse(masterUri, rId);
            masters.Add(master);
        }

        // Parse comment authors (M7)
        var commentAuthorsRel = presentationPart.Relationships
            .FirstOrDefault(static r => r.RelationshipType.Equals(
                    PmlNames.RelTypeCommentAuthors,
                    StringComparison.Ordinal
                )
            );
        if (commentAuthorsRel != null)
        {
            var caUri = presentationPart.ResolveUri(commentAuthorsRel.TargetUri);
            var caPart = package.TryGetPart(caUri);
            if (caPart != null)
            {
                var caDoc = OoXmlHelper.ParseXml(caPart.Data);
                if (caDoc.Root != null)
                    CommentAuthorParser.Parse(caDoc.Root, commentAuthors);
            }
        }

        // Parse sections (M7)
        SectionParser.Parse(root, sections);

        // Parse slide-show settings from the presProps part (M-G)
        var slideShow = ParsePresProps(presentationPart, package);
        // Parse embedded fonts (<p:embeddedFontLst>) so the renderer can use the real typefaces
        ParseEmbeddedFonts(root, presentationPart, package, mediaStore);

        // Parse slides
        var slideParser = new SlideParser(package, mediaStore, ToList(masters), commentAuthors);
        // ReSharper disable once LoopCanBePartlyConvertedToQuery
        foreach (var slideIdEl in root.Elements(PmlNames.SlideIdList)
                     .SelectMany(static l => l.Elements(PmlNames.SlideId)))
        {
            var id = (uint)(slideIdEl.GetAttrInt(PmlNames.AttributeId) ?? 256);
            var rId = (string?)slideIdEl.Attribute(PmlNames.RelationshipId);
            if (rId == null) continue;

            var slideRel = presentationPart.Relationships
                .FirstOrDefault(r =>
                    r.Id.Equals(rId, StringComparison.Ordinal)
                );

            if (slideRel == null) continue;

            var slideUri = presentationPart.ResolveUri(slideRel.TargetUri);
            var slide = slideParser.Parse(slideUri, rId, id);
            slides.AddParsed(slide);
        }

        // Update statistics
        properties.SlideCount = slides.Count;
        properties.HiddenSlideCount = slides.Count(static s => s.IsHidden);

        // Resolve internal slide-jump hyperlinks (part URI captured at parse time → slide number).
        ResolveSlideJumpHyperlinks(slides);

        // Capture verbatim-preserved content (VBA project, digital signatures) for round-trip.
        var preserved = CapturePreservedContent(package, presentationPart);

        return new ParsedPresentation(
            package,
            slides,
            masters,
            mediaStore,
            properties,
            protection,
            slideSize,
            commentAuthors,
            sections
        )
        {
            SlideShow = slideShow,
            Preserved = preserved
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Captures parts that Unchained does not model but must round-trip verbatim: the VBA macro
    ///     project (referenced from <c>presentation.xml</c>) and the digital-signature parts
    ///     (referenced from the package root). Their relationships are captured too so the writer can
    ///     re-link them.
    /// </summary>
    private static PreservedContent CapturePreservedContent(
        OpcPackage package,
        OpcPart presentationPart
    )
    {
        var preserved = new PreservedContent();

        // VBA project — a presentation-level relationship to /ppt/vbaProject.bin.
        foreach (var rel in presentationPart.Relationships.Where(static r => r.RelationshipType.Equals(PmlNames.RelTypeVbaProject, StringComparison.Ordinal)))
        {
            var uri = presentationPart.ResolveUri(rel.TargetUri);
            var part = package.TryGetPart(uri);
            if (part == null) continue;

            preserved.HasMacros = true;
            preserved.Parts.Add(
                new PreservedPart
                {
                    Uri = uri,
                    ContentType = part.ContentType,
                    Data = part.Data
                }
            );
            preserved.AnchorRelationships.Add(
                new PreservedRelationship
                {
                    SourceUri = null, // anchored to presentation.xml; writer supplies the source
                    Id = rel.Id,
                    Type = rel.RelationshipType,
                    Target = uri,
                    IsExternal = rel.IsExternal
                }
            );
        }

        // Digital signatures — a package-level origin relationship plus the signature parts it
        // links to. Captured verbatim; any edit to the deck invalidates them in PowerPoint, but a
        // pure round-trip keeps the bytes intact.
        foreach (var originRel in package.PackageRelationships.Where(static originRel =>
                     originRel.RelationshipType.Equals(PmlNames.RelTypeDigitalSignatureOrigin, StringComparison.Ordinal)
                 ))
        {
            var originUri = "/" + originRel.TargetUri.TrimStart('/');
            var originPart = package.TryGetPart(originUri);
            if (originPart == null) continue;

            CapturePartTree(package, originPart, originUri, preserved);
            preserved.AnchorRelationships.Add(
                new PreservedRelationship
                {
                    SourceUri = string.Empty, // package-level anchor
                    Id = originRel.Id,
                    Type = originRel.RelationshipType,
                    Target = originUri,
                    IsExternal = originRel.IsExternal
                }
            );
        }

        return preserved;
    }

    /// <summary>
    ///     Captures <paramref name="part" /> and, recursively, every internal part it relates to,
    ///     keeping each part's relationships verbatim. Used for the signature origin → signature parts.
    /// </summary>
    private static void CapturePartTree(
        OpcPackage package,
        OpcPart part,
        string partUri,
        PreservedContent preserved
    )
    {
        if (preserved.Parts.Any(p => p.Uri.Equals(partUri, StringComparison.OrdinalIgnoreCase)))
            return;

        var captured = new PreservedPart
        {
            Uri = partUri,
            ContentType = part.ContentType,
            Data = part.Data
        };

        foreach (var rel in part.Relationships)
        {
            captured.Relationships.Add(
                new PreservedRelationship
                {
                    SourceUri = partUri,
                    Id = rel.Id,
                    Type = rel.RelationshipType,
                    Target = rel.TargetUri,
                    IsExternal = rel.IsExternal
                }
            );

            if (rel.IsExternal) continue;

            var childUri = part.ResolveUri(rel.TargetUri);
            var childPart = package.TryGetPart(childUri);
            if (childPart != null)
                CapturePartTree(package, childPart, childUri, preserved);
        }

        preserved.Parts.Add(captured);
    }

    private static SlideShowSettings? ParsePresProps(OpcPart presentationPart, OpcPackage package)
    {
        var rel = presentationPart.Relationships.FirstOrDefault(static r =>
            r.RelationshipType.Equals(PmlNames.RelTypePresProps, StringComparison.Ordinal)
        );
        if (rel == null) return null;

        var uri = presentationPart.ResolveUri(rel.TargetUri);
        var part = package.TryGetPart(uri);
        if (part == null) return null;

        var doc = OoXmlHelper.ParseXml(part.Data);
        var showPr = doc.Root?.Element(PmlNames.Pml + "showPr");
        if (showPr == null) return null;

        var pml = PmlNames.Pml;
        var settings = new SlideShowSettings();

        if (showPr.Element(pml + "browse") != null)
            settings.ShowType = SlideShowType.Browsed;
        else if (showPr.Element(pml + "kiosk") != null)
            settings.ShowType = SlideShowType.Kiosk;
        else
            settings.ShowType = SlideShowType.Presenter;

        settings.Loop = showPr.GetAttrBool("loop") ?? false;
        // XML stores positive sense; absence means "show". Our model stores the inverse.
        settings.ShowWithoutNarration = showPr.GetAttrBool("showNarration") == false;
        settings.ShowWithoutAnimation = showPr.GetAttrBool("showAnimation") == false;

        var sldRg = showPr.Element(pml + "sldRg");
        if (sldRg != null)
        {
            settings.RangeStart = sldRg.GetAttrInt("st");
            settings.RangeEnd = sldRg.GetAttrInt("end");
        }

        var penClr = showPr.Element(pml + "penClr");
        var srgb = penClr?.Element(DmlNames.SolidFill)?.Element(DmlNames.SrgbColor);
        settings.PenColorHex = (string?)srgb?.Attribute(DmlNames.AttributeValue);

        return settings;
    }

    private static void ResolveSlideJumpHyperlinks(SlideCollection slides)
    {
        // Map each slide's part URI to its 1-based number.
        var numberByUri = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < slides.Count; i++)
        {
            if (!string.IsNullOrEmpty(slides[i].PartUri))
                numberByUri[slides[i].PartUri] = i + 1;
        }

        foreach (var action in from slide in slides from shape in EnumerateAllShapes(slide.Shapes) select shape.ClickAction)
        {
            if (action?.TargetSlidePartUri is { } uri && numberByUri.TryGetValue(uri, out var number))
                action.TargetSlideNumber = number;
        }

        // Run-level slide-jump links.
        foreach (var link in slides.SelectMany(static slide => ShapeTextWalker.EnumerateTextFrames(slide.Shapes)
                     .SelectMany(static frame => frame.Paragraphs
                         .SelectMany(static paragraph => paragraph.Runs
                             .Select(static run => run.Format.Hyperlink)
                         )
                     )
                 ))
        {
            if (link?.TargetPartUri is { } uri && numberByUri.TryGetValue(uri, out var number))
                link.TargetSlideNumber = number;
        }
    }

    private static IEnumerable<Shape> EnumerateAllShapes(IEnumerable<Shape> shapes)
    {
        foreach (var shape in shapes)
        {
            yield return shape;

            if (shape is not GroupShape group) continue;

            foreach (var child in EnumerateAllShapes(group.Children))
                yield return child;
        }
    }

    private static void ParseEmbeddedFonts(
        XContainer root,
        OpcPart presentationPart,
        OpcPackage package,
        MediaStore mediaStore
    )
    {
        var listEl = root.Element(PmlNames.EmbeddedFontList);
        if (listEl == null) return;

        var rNs = PmlNames.Relationships;

        foreach (var fontEl in listEl.Elements(PmlNames.EmbeddedFont))
        {
            var typeface = fontEl.Element(PmlNames.Font)?.Attribute(PmlNames.AttributeTypeface)?.Value;
            if (string.IsNullOrEmpty(typeface)) continue;

            AddVariant(fontEl.Element(PmlNames.FontRegular), EmbeddedFontStyle.Regular);
            AddVariant(fontEl.Element(PmlNames.FontBold), EmbeddedFontStyle.Bold);
            AddVariant(fontEl.Element(PmlNames.FontItalic), EmbeddedFontStyle.Italic);
            AddVariant(fontEl.Element(PmlNames.FontBoldItalic), EmbeddedFontStyle.BoldItalic);
            continue;

            void AddVariant(XElement? variantEl, EmbeddedFontStyle style)
            {
                var rId = (string?)variantEl?.Attribute(rNs + "id");
                if (string.IsNullOrEmpty(rId)) return;

                var rel = presentationPart.Relationships
                    .FirstOrDefault(r => r.Id.Equals(rId, StringComparison.Ordinal));
                if (rel == null) return;

                var fontUri = presentationPart.ResolveUri(rel.TargetUri);
                var part = package.TryGetPart(fontUri);
                if (part == null) return;

                mediaStore.AddFont(
                    new EmbeddedFont
                    {
                        Typeface = typeface,
                        Style = style,
                        Data = part.Data
                    }
                );
            }
        }
    }

    private static ProtectionInfo ParseProtection(XContainer root)
    {
        var protection = new ProtectionInfo();
        var pml = PmlNames.Pml;

        var modVerEl = root.Element(pml + "modifyVerifier");
        if (modVerEl == null) return protection;

        protection.WriteProtectionSaltBase64 = modVerEl.GetAttr("saltValue");
        protection.WriteProtectionHashBase64 = modVerEl.GetAttr("hashValue");

        return protection;
    }

    private static SlideSize ParseSlideSize(XContainer root)
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
                    PmlNames.RelTypeCoreProperties,
                    StringComparison.Ordinal
                )
            );

        if (coreRel == null) return props;

        var corePart = package.TryGetPart("/" + coreRel.TargetUri.TrimStart('/'));
        if (corePart != null)
            ParseCoreProperties(corePart.Data, props);

        return props;
    }

    private static void ParseCoreProperties(byte[] data, OoXmlCoreProperties props)
    {
        try
        {
            var doc = OoXmlHelper.ParseXml(data);
            var root = doc.Root;
            if (root == null) return;

            var dc = XNamespace.Get("http://purl.org/dc/elements/1.1/");
            var cp = XNamespace.Get(
                "http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
            );
            var dcterms = XNamespace.Get("http://purl.org/dc/terms/");

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

    private static IEnumerable<MasterSlide> ToList(MasterSlideCollection collection)
    {
        var list = new List<MasterSlide>(collection.Count);
        list.AddRange(collection);

        return list;
    }
}

/// <summary>
///     The result of parsing a PPTX package — all components needed to construct
///     the public presentation adapter.
/// </summary>
internal sealed class ParsedPresentation(
    OpcPackage? package,
    SlideCollection slides,
    MasterSlideCollection masters,
    MediaStore mediaStore,
    DocumentProperties properties,
    ProtectionInfo protection,
    SlideSize slideSize,
    CommentAuthorCollection commentAuthors,
    SectionCollection sections
)
{
    /// <summary>
    ///     The source OPC package, when parsed via the custom OPC layer. <see langword="null" />
    ///     when the presentation was read through the OpenXML-SDK-backed engine (which owns its
    ///     own package). Not consumed downstream of construction.
    /// </summary>
    public OpcPackage? Package { get; } = package;

    public SlideCollection Slides { get; } = slides;
    public MasterSlideCollection Masters { get; } = masters;
    public MediaStore MediaStore { get; } = mediaStore;
    public DocumentProperties Properties { get; } = properties;
    public ProtectionInfo Protection { get; } = protection;
    public SlideSize SlideSize { get; } = slideSize;
    public CommentAuthorCollection CommentAuthors { get; } = commentAuthors;
    public SectionCollection Sections { get; } = sections;

    /// <summary>
    ///     The still-open OpenXML-SDK engine when parsed via the SDK path; <see langword="null" />
    ///     for the custom-OPC path. The document takes ownership and disposes it, keeping the source
    ///     package alive for an in-place SDK-backed save.
    /// </summary>
    public OoxmlEngine? Engine { get; init; }

    /// <summary>Slide-show settings parsed from the <c>presProps.xml</c> part, if present.</summary>
    public SlideShowSettings? SlideShow { get; init; }

    /// <summary>Verbatim-preserved content (VBA project, digital signatures) for round-trip.</summary>
    public PreservedContent? Preserved { get; init; }
}
