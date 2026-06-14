using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Text;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Comments;
using Unchained.Pptx.Core;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Parsing;
using Unchained.Pptx.Security;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using SaveOptions = Unchained.Pptx.Models.SaveOptions;

namespace Unchained.Pptx.Writing;

/// <summary>
///     Serializes the Unchained presentation object model back into a valid PPTX
///     (OPC/ZIP) package. This is the top-level entry point for the M1–M4 write path.
/// </summary>
internal sealed class PresentationWriter
{
    // ── Part URI templates ────────────────────────────────────────────────────

    private const string PresentationPartUri = "/ppt/presentation.xml";
    private const string CorePropsPartUri = "/docProps/core.xml";

    // ── Comment authors (M7) ─────────────────────────────────────────────────

    private const string CommentAuthorsPartUri = "/ppt/commentAuthors.xml";

    // ── Presentation properties (slide-show settings, M-G) ───────────────────────

    private const string PresPropsPartUri = "/ppt/presProps.xml";

    /// <summary>
    ///     Serializes <paramref name="slides" />, <paramref name="masters" />,
    ///     and supporting data into a new <see cref="OpcPackage" /> and returns the
    ///     raw PPTX bytes.
    /// </summary>
    public static byte[] Write(
        SlideCollection slides,
        MasterSlideCollection masters,
        MediaStore mediaStore,
        DocumentProperties properties,
        SlideSize slideSize,
        CommentAuthorCollection? commentAuthors = null,
        SectionCollection? sections = null,
        ProtectionInfo? protection = null,
        SaveOptions? options = null,
        SlideShowSettings? slideShow = null,
        PreservedContent? preserved = null
    )
    {
        var package = OpcPackage.CreateEmpty();

        // Register standard defaults
        var contentTypes = new ContentTypeMap();
        contentTypes.RegisterDefault(
            "rels",
            "application/vnd.openxmlformats-package.relationships+xml"
        );
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
        WriteMedia(package, mediaStore);

        // Write masters + their themes + layouts
        WriteMasters(package, masters, contentTypes);

        // Write slides (chart part index + notes/comment index shared across slides)
        var chartPartIndex = 1;
        var notesPartIndex = 1;
        var commentPartIndex = 1;
        WriteSlides(
            package,
            slides,
            contentTypes,
            ref chartPartIndex,
            ref notesPartIndex,
            ref commentPartIndex
        );

        // Write comment authors (M7) — before presentation.xml so we can add relationship
        var hasAnyComments = slides.Any(static s => s.HasComments);
        if (hasAnyComments || commentAuthors is { Count: > 0 })
        {
            var authors = commentAuthors ?? new CommentAuthorCollection();
            WriteCommentAuthors(package, authors, contentTypes);
        }

        // Write presentation-properties part (slide-show settings) when any are set (M-G).
        var hasPresProps = slideShow?.HasAnySetting == true;
        if (hasPresProps)
            WritePresProps(package, slideShow!, contentTypes);

        // Write presentation.xml (includes sections and write-protection). Use the macro-enabled
        // content type when a VBA project is being preserved (M-G).
        var hasMacros = preserved?.HasMacros == true;
        WritePresentationPart(
            package,
            slides,
            masters,
            slideSize,
            sections,
            protection,
            contentTypes,
            hasPresProps,
            hasMacros
        );

        // Re-emit verbatim-preserved content (VBA project, digital signatures) (M-G).
        if (preserved is { IsEmpty: false })
            WritePreservedContent(package, preserved, contentTypes);

        // Write core properties
        WriteCoreProperties(package, properties, contentTypes);

        // Package-level relationships
        package.AddPackageRelationship(
            "rId1",
            PmlNames.RelTypePresentation,
            PresentationPartUri.TrimStart('/')
        );
        package.AddPackageRelationship(
            "rId2",
            PmlNames.RelTypeCoreProperties,
            CorePropsPartUri.TrimStart('/')
        );

        var zipBytes = package.Save();

        // Encrypt output if password was provided (M8)
        return !string.IsNullOrEmpty(options?.Password) ? AgileEncryption.Encrypt(zipBytes, options.Password) : zipBytes;
    }

    // ── Media ─────────────────────────────────────────────────────────────────

    private static void WriteMedia(
        OpcPackage package,
        MediaStore mediaStore
    )
    {
        var index = 1;

        foreach (var image in mediaStore.Images)
        {
            var extension = ExtensionForContentType(image.ContentType);
            var uri = $"/ppt/media/image{index++}{extension}";
            image.PartUri = uri;
            package.AddOrReplacePart(uri, image.ContentType, image.Data.ToArray());
        }
    }

    // ── Masters ───────────────────────────────────────────────────────────────

    private static void WriteMasters(
        OpcPackage package,
        MasterSlideCollection masters,
        ContentTypeMap contentTypes
    )
    {
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
            package.AddOrReplacePart(
                themeUri,
                PmlNames.ContentTypeTheme,
                new XDocument(themeXml).ToUtf8Bytes()
            );
            contentTypes.Register(themeUri, PmlNames.ContentTypeTheme);

            // Write layouts
            var layoutUris = WriteLayouts(package, master.Layouts, i + 1, contentTypes);

            // Write master XML
            var masterXml = MasterWriter.Write(master, themeUri, layoutUris);
            package.AddOrReplacePart(
                masterUri,
                PmlNames.ContentTypeSlideMaster,
                new XDocument(masterXml).ToUtf8Bytes()
            );

            // Master relationships. Layout rels reuse layout.RelationshipId (rId1..N), so the
            // theme rel must take an ID past that range to avoid colliding on the master part.
            package.AddRelationship(
                masterUri,
                $"rId{layoutUris.Count + 1}",
                PmlNames.RelTypeTheme,
                themeUri
            );
            foreach (var (layout, layoutUri) in layoutUris)
            {
                package.AddRelationship(
                    masterUri,
                    layout.RelationshipId.Length > 0 ? layout.RelationshipId : "rId1",
                    PmlNames.RelTypeSlideLayout,
                    RelativeUri(masterUri, layoutUri)
                );
            }
        }
    }

    private static Dictionary<SlideLayout, string> WriteLayouts(
        OpcPackage package,
        SlideLayoutCollection layouts,
        int masterIndex,
        ContentTypeMap contentTypes
    )
    {
        var layoutUris = new Dictionary<SlideLayout, string>();
        var rIdCounter = 1;

        for (var j = 0; j < layouts.Count; j++)
        {
            var layout = layouts[j];
            var layoutUri = string.IsNullOrEmpty(layout.PartUri)
                ? $"/ppt/slideLayouts/slideLayout{((masterIndex - 1) * 20) + j + 1}.xml"
                : layout.PartUri;

            layout.PartUri = layoutUri;
            if (string.IsNullOrEmpty(layout.RelationshipId))
                layout.RelationshipId = $"rId{rIdCounter++}";

            contentTypes.Register(layoutUri, PmlNames.ContentTypeSlideLayout);

            var layoutXml = LayoutWriter.Write(layout);
            package.AddOrReplacePart(
                layoutUri,
                PmlNames.ContentTypeSlideLayout,
                new XDocument(layoutXml).ToUtf8Bytes()
            );

            // Layout → master relationship
            package.AddRelationship(
                layoutUri,
                "rId1",
                PmlNames.RelTypeSlideMaster,
                RelativeUri(layoutUri, layout.Master.PartUri)
            );

            layoutUris[layout] = layoutUri;
        }

        return layoutUris;
    }

    // ── Slides ────────────────────────────────────────────────────────────────

    private static void WriteSlides(
        OpcPackage package,
        SlideCollection slides,
        ContentTypeMap contentTypes,
        ref int chartPartIndex,
        ref int notesPartIndex,
        ref int commentPartIndex
    )
    {
        // Pre-assign every slide's part URI first, so internal slide-jump hyperlinks can resolve
        // their target (which may be a later slide) while writing each slide's relationships.
        for (var i = 0; i < slides.Count; i++)
        {
            if (string.IsNullOrEmpty(slides[i].PartUri))
                slides[i].PartUri = $"/ppt/slides/slide{i + 1}.xml";
        }

        foreach (var slide in slides)
        {
            var slideUri = slide.PartUri;

            // Pre-assign all relationship IDs before generating the slide XML,
            // so that shape writers can embed the correct rId values.
            var rId = 2; // rId1 is reserved for the layout relationship

            foreach (var shape in slide.Shapes.OfType<PictureShape>()
                         .Where(static p => p.Image is { PartUri.Length: > 0 })
                         .Where(static shape => string.IsNullOrEmpty(shape.Image!.RelationshipId)))
                shape.Image!.RelationshipId = $"rId{rId++}";

            foreach (var chartShape in slide.Shapes.OfType<ChartShape>())
            {
                if (string.IsNullOrEmpty(chartShape.PartUri))
                    chartShape.PartUri = $"/ppt/charts/chart{chartPartIndex++}.xml";
                if (string.IsNullOrEmpty(chartShape.RelationshipId))
                    chartShape.RelationshipId = $"rId{rId++}";
            }

            // Assign relationship IDs for shape click-hyperlinks (recursing through groups).
            foreach (var shape in EnumerateAllShapes(slide.Shapes))
            {
                if (shape.ClickAction is { } action && NeedsRelationship(action)
                                                    && string.IsNullOrEmpty(action.RelationshipId))
                    action.RelationshipId = $"rId{rId++}";
            }

            // Assign relationship IDs for run-level hyperlinks across all text frames.
            foreach (var link in EnumerateRunHyperlinks(slide)
                         .Where(static link => RunLinkNeedsRelationship(link) && string.IsNullOrEmpty(link.RelationshipId)))
                link.RelationshipId = $"rId{rId++}";

            // Write slide XML (all relationship IDs now set)
            contentTypes.Register(slideUri, PmlNames.ContentTypeSlide);
            var slideXml = SlideWriter.Write(slide);
            package.AddOrReplacePart(
                slideUri,
                PmlNames.ContentTypeSlide,
                new XDocument(slideXml).ToUtf8Bytes()
            );

            // Slide relationships
            package.AddRelationship(
                slideUri,
                "rId1",
                PmlNames.RelTypeSlideLayout,
                RelativeUri(slideUri, slide.Layout.PartUri)
            );

            foreach (var shape in slide.Shapes.OfType<PictureShape>()
                         .Where(static p => p.Image is { PartUri.Length: > 0 }))
            {
                package.AddRelationship(
                    slideUri,
                    shape.Image!.RelationshipId,
                    PmlNames.RelTypeImage,
                    RelativeUri(slideUri, shape.Image.PartUri)
                );
            }

            foreach (var chartShape in slide.Shapes.OfType<ChartShape>())
            {
                // Use raw bytes for loaded charts (preserves workbook links);
                // generate from ChartModel for new charts.
                var chartBytes = chartShape.ChartPartData
                                 ?? ChartWriter.Write(chartShape.Chart);

                package.AddOrReplacePart(chartShape.PartUri, PmlNames.ContentTypeChart, chartBytes);
                contentTypes.Register(chartShape.PartUri, PmlNames.ContentTypeChart);

                package.AddRelationship(
                    slideUri,
                    chartShape.RelationshipId,
                    PmlNames.RelTypeChart,
                    RelativeUri(slideUri, chartShape.PartUri)
                );
            }

            // SmartArt diagrams (M-F): write the referenced diagram parts using the
            // relationship IDs/URIs preserved from load, so the graphic frame's <dgm:relIds>
            // (emitted verbatim from RawElement) continue to resolve.
            foreach (var smartArt in slide.Shapes.OfType<SmartArtShape>())
                WriteSmartArtParts(package, contentTypes, slideUri, smartArt);

            // Shape click-hyperlinks (M-G): external URLs become External relationships;
            // internal slide jumps target the resolved slide part URI.
            foreach (var shape in EnumerateAllShapes(slide.Shapes))
            {
                if (shape.ClickAction is { } action && NeedsRelationship(action))
                    WriteHyperlinkRelationship(package, slides, slideUri, action);
            }

            // Run-level hyperlinks (M-G).
            foreach (var link in EnumerateRunHyperlinks(slide).Where(RunLinkNeedsRelationship))
                WriteRunHyperlinkRelationship(package, slides, slideUri, link);

            // Notes (M7)
            if (!string.IsNullOrEmpty(slide.Notes.NotesText))
            {
                var notesUri = $"/ppt/notesSlides/notesSlide{notesPartIndex++}.xml";
                var notesDoc = NotesWriter.Write(slide.Notes);
                if (notesDoc != null)
                {
                    package.AddOrReplacePart(
                        notesUri,
                        PmlNames.ContentTypeNotesSlide,
                        notesDoc.ToUtf8Bytes()
                    );
                    contentTypes.Register(notesUri, PmlNames.ContentTypeNotesSlide);
                    package.AddRelationship(
                        slideUri,
                        $"rId{rId++}",
                        PmlNames.RelTypeNotesSlide,
                        RelativeUri(slideUri, notesUri)
                    );
                }
            }

            // Comments (M7)
            // ReSharper disable once InvertIf
            if (slide.HasComments)
            {
                var comments = slide.GetComments();
                var commentsUri = $"/ppt/comments/comment{commentPartIndex++}.xml";
                var cmDoc = CommentWriter.Write(comments);
                package.AddOrReplacePart(
                    commentsUri,
                    PmlNames.ContentTypeComments,
                    cmDoc.ToUtf8Bytes()
                );
                contentTypes.Register(commentsUri, PmlNames.ContentTypeComments);
                package.AddRelationship(
                    slideUri,
                    // ReSharper disable once RedundantAssignment
                    $"rId{rId++}",
                    PmlNames.RelTypeComments,
                    RelativeUri(slideUri, commentsUri)
                );
            }
        }
    }

    // ── Hyperlinks (M-G) ─────────────────────────────────────────────────────────

    /// <summary>Yields every shape in the collection, recursing into group children.</summary>
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

    /// <summary>True when a hyperlink action has a target that requires an OPC relationship.</summary>
    private static bool NeedsRelationship(HyperlinkAction action) =>
        !string.IsNullOrEmpty(action.Url) || action.TargetSlideNumber.HasValue;

    private static void WriteHyperlinkRelationship(
        OpcPackage package,
        SlideCollection slides,
        string slideUri,
        HyperlinkAction action
    )
    {
        if (string.IsNullOrEmpty(action.RelationshipId)) return;

        if (!string.IsNullOrEmpty(action.Url))
        {
            package.AddRelationship(
                slideUri,
                action.RelationshipId,
                PmlNames.RelTypeHyperlink,
                action.Url,
                true
            );
        }
        else if (action.TargetSlideNumber is { } number and >= 1 && number <= slides.Count)
        {
            var targetUri = slides[number - 1].PartUri;
            package.AddRelationship(
                slideUri,
                action.RelationshipId,
                PmlNames.RelTypeSlide,
                RelativeUri(slideUri, targetUri)
            );
        }
    }

    /// <summary>Yields every run-level hyperlink across a slide's text frames.</summary>
    private static IEnumerable<RunHyperlink> EnumerateRunHyperlinks(Slide slide)
    {
        foreach (var run in ShapeTextWalker.EnumerateTextFrames(slide.Shapes)
                     .SelectMany(static frame => frame.Paragraphs.SelectMany(static paragraph => paragraph.Runs)))
        {
            if (run.Format.Hyperlink is { } link)
                yield return link;
        }
    }

    private static bool RunLinkNeedsRelationship(RunHyperlink link) =>
        !string.IsNullOrEmpty(link.Url) || link.TargetSlideNumber.HasValue;

    private static void WriteRunHyperlinkRelationship(
        OpcPackage package,
        SlideCollection slides,
        string slideUri,
        RunHyperlink link
    )
    {
        if (string.IsNullOrEmpty(link.RelationshipId)) return;

        if (!string.IsNullOrEmpty(link.Url))
        {
            package.AddRelationship(
                slideUri,
                link.RelationshipId,
                PmlNames.RelTypeHyperlink,
                link.Url,
                true
            );
        }
        else if (link.TargetSlideNumber is { } number and >= 1 && number <= slides.Count)
        {
            var targetUri = slides[number - 1].PartUri;
            package.AddRelationship(
                slideUri,
                link.RelationshipId,
                PmlNames.RelTypeSlide,
                RelativeUri(slideUri, targetUri)
            );
        }
    }

    // ── SmartArt diagram parts (M-F) ─────────────────────────────────────────────
    /// <summary>
    ///     Writes the up-to-five OPC parts backing a SmartArt diagram (data, layout, quick-style,
    ///     colours, and the Microsoft pre-rendered drawing) plus the slide relationships that
    ///     reference them. Relationship IDs and part URIs are reused from load so the graphic
    ///     frame's preserved <c>&lt;dgm:relIds&gt;</c> remain valid. Any text edits on the node model
    ///     are applied back onto the data part before writing.
    /// </summary>
    private static void WriteSmartArtParts(
        OpcPackage package,
        ContentTypeMap contentTypes,
        string slideUri,
        SmartArtShape shape
    )
    {
        // Data part — apply node-text edits onto the preserved data document, if present.
        var dataBytes = shape.DataPartData;
        if (shape.DiagramDataDocument?.Root != null)
        {
            SmartArtParser.ApplyTextEdits(shape.DiagramDataDocument.Root, shape.Nodes);
            dataBytes = shape.DiagramDataDocument.ToUtf8Bytes();
        }

        WriteDiagramPart(
            package,
            contentTypes,
            slideUri,
            shape.DataRelationshipId,
            shape.DataPartUri,
            dataBytes,
            PmlNames.RelTypeDiagramData,
            PmlNames.ContentTypeDiagramData
        );

        WriteDiagramPart(
            package,
            contentTypes,
            slideUri,
            shape.LayoutRelationshipId,
            shape.LayoutPartUri,
            shape.LayoutPartData,
            PmlNames.RelTypeDiagramLayout,
            PmlNames.ContentTypeDiagramLayout
        );

        WriteDiagramPart(
            package,
            contentTypes,
            slideUri,
            shape.QuickStyleRelationshipId,
            shape.QuickStylePartUri,
            shape.QuickStylePartData,
            PmlNames.RelTypeDiagramQuickStyle,
            PmlNames.ContentTypeDiagramQuickStyle
        );

        WriteDiagramPart(
            package,
            contentTypes,
            slideUri,
            shape.ColorsRelationshipId,
            shape.ColorsPartUri,
            shape.ColorsPartData,
            PmlNames.RelTypeDiagramColors,
            PmlNames.ContentTypeDiagramColors
        );

        WriteDiagramPart(
            package,
            contentTypes,
            slideUri,
            shape.DrawingRelationshipId,
            shape.DrawingPartUri,
            shape.DrawingPartData,
            PmlNames.RelTypeDiagramDrawing,
            PmlNames.ContentTypeDiagramDrawing
        );
    }

    private static void WriteDiagramPart(
        OpcPackage package,
        ContentTypeMap contentTypes,
        string slideUri,
        string relationshipId,
        string partUri,
        byte[]? data,
        string relType,
        string contentType
    )
    {
        if (string.IsNullOrEmpty(relationshipId) || string.IsNullOrEmpty(partUri) || data == null)
            return;

        package.AddOrReplacePart(partUri, contentType, data);
        contentTypes.Register(partUri, contentType);
        package.AddRelationship(slideUri, relationshipId, relType, RelativeUri(slideUri, partUri));
    }

    private static void WriteCommentAuthors(
        OpcPackage package,
        CommentAuthorCollection authors,
        ContentTypeMap contentTypes
    )
    {
        var caDoc = CommentAuthorWriter.Write(authors);
        package.AddOrReplacePart(
            CommentAuthorsPartUri,
            PmlNames.ContentTypeCommentAuthors,
            caDoc.ToUtf8Bytes()
        );
        contentTypes.Register(CommentAuthorsPartUri, PmlNames.ContentTypeCommentAuthors);
    }

    // ── Presentation part ─────────────────────────────────────────────────────

    private static void WritePresentationPart(
        OpcPackage package,
        SlideCollection slides,
        MasterSlideCollection masters,
        SlideSize slideSize,
        SectionCollection? sections,
        ProtectionInfo? protection,
        ContentTypeMap contentTypes,
        bool hasPresProps = false,
        bool hasMacros = false
    )
    {
        var pml = PmlNames.Pml;
        var r = PmlNames.Relationships;

        var pres = new XElement(
            PmlNames.Presentation,
            new XAttribute(XNamespace.Xmlns + "p", pml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName)
        );

        // Slide master IDs
        var masterIdLst = new XElement(PmlNames.SlideMasterIdList);
        for (var i = 0; i < masters.Count; i++)
        {
            var master = masters[i];
            masterIdLst.Add(
                new XElement(
                    PmlNames.SlideMasterId,
                    new XAttribute(PmlNames.AttributeId, (uint)(2_147_483_648 + i)),
                    new XAttribute(
                        PmlNames.RelationshipId,
                        master.RelationshipId.Length > 0
                            ? master.RelationshipId
                            : $"rId{100 + i}"
                    )
                )
            );
        }

        pres.Add(masterIdLst);

        // Slide size
        pres.Add(
            new XElement(
                PmlNames.SlideSize,
                new XAttribute(PmlNames.AttributeWidth, slideSize.Width.Value),
                new XAttribute(PmlNames.AttributeHeight, slideSize.Height.Value)
            )
        );

        pres.Add(
            new XElement(
                PmlNames.NotesSize,
                new XAttribute(PmlNames.AttributeWidth, Emu.FromInches(7.5).Value),
                new XAttribute(PmlNames.AttributeHeight, Emu.FromInches(10).Value)
            )
        );

        // Slide IDs
        var slideIdLst = new XElement(PmlNames.SlideIdList);
        for (var i = 0; i < slides.Count; i++)
        {
            var slide = slides[i];
            slideIdLst.Add(
                new XElement(
                    PmlNames.SlideId,
                    new XAttribute(PmlNames.AttributeId, slide.SlideId),
                    new XAttribute(
                        PmlNames.RelationshipId,
                        slide.RelationshipId.Length > 0 ? slide.RelationshipId : $"rId{i + 1}"
                    )
                )
            );
        }

        pres.Add(slideIdLst);

        // Write-protection (M8)
        if (protection?.IsWriteProtected == true)
            pres.Add(WriteModifyVerifier(protection));

        // Sections extension (M7 — PowerPoint 2010+)
        if (sections is { Count: > 0 })
            pres.Add(WriteSectionsExtLst(sections));

        var presXml = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            pres
        );

        // A presentation that carries a VBA project must use the macro-enabled content type.
        var presContentType = hasMacros
            ? PmlNames.ContentTypePresentationMacroEnabled
            : PmlNames.ContentTypePresentation;
        package.AddOrReplacePart(PresentationPartUri, presContentType, presXml.ToUtf8Bytes());
        contentTypes.Register(PresentationPartUri, presContentType);

        // Presentation relationships
        var rIdCounter = 1;
        foreach (var master in Enumerable.Range(0, masters.Count).Select(i => masters[i]))
        {
            var rId = master.RelationshipId.Length > 0
                ? master.RelationshipId
                : $"rId{100 + rIdCounter}";
            master.RelationshipId = rId;
            package.AddRelationship(
                PresentationPartUri,
                rId,
                PmlNames.RelTypeSlideMaster,
                RelativeUri(PresentationPartUri, master.PartUri)
            );
            rIdCounter++;
        }

        for (var i = 0; i < slides.Count; i++)
        {
            var slide = slides[i];
            var rId = slide.RelationshipId.Length > 0
                ? slide.RelationshipId
                : $"rId{i + 1}";
            slide.RelationshipId = rId;
            package.AddRelationship(
                PresentationPartUri,
                rId,
                PmlNames.RelTypeSlide,
                RelativeUri(PresentationPartUri, slide.PartUri)
            );
        }

        // Comment authors relationship (M7)
        if (package.TryGetPart(CommentAuthorsPartUri) != null)
        {
            package.AddRelationship(
                PresentationPartUri,
                $"rId{rIdCounter++}",
                PmlNames.RelTypeCommentAuthors,
                RelativeUri(PresentationPartUri, CommentAuthorsPartUri)
            );
        }

        // Presentation-properties relationship (M-G slide-show settings)
        if (hasPresProps && package.TryGetPart(PresPropsPartUri) != null)
        {
            package.AddRelationship(
                PresentationPartUri,
                // ReSharper disable once RedundantAssignment
                $"rId{rIdCounter++}",
                PmlNames.RelTypePresProps,
                RelativeUri(PresentationPartUri, PresPropsPartUri)
            );
        }
    }

    // ── Preserved content (VBA + digital signatures, M-G) ────────────────────────

    /// <summary>
    ///     Re-emits verbatim-preserved parts (VBA project, digital-signature parts) and re-links them.
    ///     Each preserved part keeps its original relationships; anchor relationships to
    ///     <c>presentation.xml</c> / the package root are added with fresh ids to avoid collisions.
    /// </summary>
    private static void WritePreservedContent(
        OpcPackage package,
        PreservedContent preserved,
        ContentTypeMap contentTypes
    )
    {
        // Write each preserved part and its own (verbatim) relationships.
        foreach (var part in preserved.Parts)
        {
            package.AddOrReplacePart(part.Uri, part.ContentType, part.Data);
            contentTypes.Register(part.Uri, part.ContentType);
        }

        foreach (var part in preserved.Parts)
        foreach (var rel in part.Relationships)
            package.AddRelationship(part.Uri, rel.Id, rel.Type, rel.Target, rel.IsExternal);

        // Anchor relationships connect a known source to a preserved part. Use fresh ids so they
        // do not collide with relationships the writer already added to presentation.xml.
        var presRelIndex = 1000;
        foreach (var anchor in preserved.AnchorRelationships)
        {
            if (anchor.SourceUri is null)
            {
                // presentation.xml → VBA project (or similar).
                package.AddRelationship(
                    PresentationPartUri,
                    $"rId{presRelIndex++}",
                    anchor.Type,
                    RelativeUri(PresentationPartUri, anchor.Target),
                    anchor.IsExternal
                );
            }
            else if (anchor.SourceUri.Length == 0)
            {
                // Package root → signature origin.
                package.AddPackageRelationship(anchor.Id, anchor.Type, anchor.Target.TrimStart('/'));
            }
        }
    }

    private static void WritePresProps(
        OpcPackage package,
        SlideShowSettings show,
        ContentTypeMap contentTypes
    )
    {
        var pml = PmlNames.Pml;

        var showPr = new XElement(pml + "showPr");

        // Show type is a child element choice: present / browse / kiosk.
        showPr.Add(
            show.ShowType switch
            {
                SlideShowType.Browsed => new XElement(pml + "browse"),
                SlideShowType.Kiosk => new XElement(pml + "kiosk"),
                _ => new XElement(pml + "present")
            }
        );

        // Optional slide range (1-based, inclusive).
        if (show.RangeStart.HasValue || show.RangeEnd.HasValue)
        {
            var start = show.RangeStart ?? 1;
            var end = show.RangeEnd ?? start;
            showPr.Add(
                new XElement(
                    pml + "sldRg",
                    new XAttribute("st", start),
                    new XAttribute("end", end)
                )
            );
        }

        if (!string.IsNullOrEmpty(show.PenColorHex))
        {
            showPr.Add(
                new XElement(
                    pml + "penClr",
                    new XElement(
                        DmlNames.SolidFill,
                        new XElement(
                            DmlNames.SrgbColor,
                            new XAttribute("val", show.PenColorHex)
                        )
                    )
                )
            );
        }

        // Boolean show flags are attributes on showPr. The XML stores the positive sense
        // (showNarration / showAnimation default true), so invert our "without" booleans.
        if (show.Loop) showPr.Add(new XAttribute("loop", "1"));
        if (show.ShowWithoutNarration) showPr.Add(new XAttribute("showNarration", "0"));
        if (show.ShowWithoutAnimation) showPr.Add(new XAttribute("showAnimation", "0"));

        var presentationPr = new XElement(
            pml + "presentationPr",
            new XAttribute(XNamespace.Xmlns + "p", pml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "a", DmlNames.Dml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", PmlNames.Relationships.NamespaceName),
            showPr
        );

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), presentationPr);
        package.AddOrReplacePart(PresPropsPartUri, PmlNames.ContentTypePresProps, doc.ToUtf8Bytes());
        contentTypes.Register(PresPropsPartUri, PmlNames.ContentTypePresProps);
    }

    private static XElement WriteModifyVerifier(ProtectionInfo protection)
    {
        var pml = PmlNames.Pml;
        return new XElement(
            pml + "modifyVerifier",
            new XAttribute("cryptProviderType", "rsaAES"),
            new XAttribute("cryptAlgorithmClass", "hash"),
            new XAttribute("cryptAlgorithmType", "typeAny"),
            new XAttribute("cryptAlgorithmSid", "14"), // SHA-512
            new XAttribute("spinCount", ProtectionInfo.WriteProtectionSpinCount),
            new XAttribute("saltValue", protection.WriteProtectionSaltBase64!),
            new XAttribute("hashValue", protection.WriteProtectionHashBase64!)
        );
    }

    private static XElement WriteSectionsExtLst(SectionCollection sections)
    {
        var pml = PmlNames.Pml;
        var p14 = XNamespace.Get("http://schemas.microsoft.com/office/powerpoint/2010/main");

        var sectionLst = new XElement(p14 + "sectionLst");

        foreach (var section in sections)
        {
            var sec = new XElement(
                p14 + "section",
                new XAttribute("name", section.Name),
                new XAttribute("id", $"{{{Guid.NewGuid()}}}")
            );

            var sldIdLst = new XElement(p14 + "sldIdLst");
            foreach (var id in section.SlideIds)
                sldIdLst.Add(new XElement(p14 + "sldId", new XAttribute("id", id)));
            sec.Add(sldIdLst);
            sectionLst.Add(sec);
        }

        var ext = new XElement(
            pml + "ext",
            new XAttribute("uri", "{521415D9-36F7-43E2-AB2F-B90AF26B5E84}"),
            sectionLst
        );

        return new XElement(pml + "extLst", ext);
    }

    // ── Core properties ───────────────────────────────────────────────────────

    private static void WriteCoreProperties(
        OpcPackage package,
        DocumentProperties properties,
        ContentTypeMap contentTypes
    )
    {
        var cp = XNamespace.Get(
            "http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
        );
        var dc = XNamespace.Get("http://purl.org/dc/elements/1.1/");
        var dcterms = XNamespace.Get("http://purl.org/dc/terms/");
        var xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

        var root = new XElement(
            cp + "coreProperties",
            new XAttribute(XNamespace.Xmlns + "cp", cp.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dcterms", dcterms.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName)
        );

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
        root.Add(
            new XElement(
                dcterms + "created",
                new XAttribute(xsi + "type", "dcterms:W3CDTF"),
                (properties.Created ?? now).ToString("yyyy-MM-ddTHH:mm:ssZ")
            )
        );
        root.Add(
            new XElement(
                dcterms + "modified",
                new XAttribute(xsi + "type", "dcterms:W3CDTF"),
                (properties.Modified ?? now).ToString("yyyy-MM-ddTHH:mm:ssZ")
            )
        );

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        package.AddOrReplacePart(
            CorePropsPartUri,
            PmlNames.ContentTypeCoreProperties,
            doc.ToUtf8Bytes()
        );
        contentTypes.Register(CorePropsPartUri, PmlNames.ContentTypeCoreProperties);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Returns a relative URI from <paramref name="sourceUri" /> to <paramref name="targetUri" />.
    ///     Both must be absolute OPC part URIs starting with '/'.
    /// </summary>
    private static string RelativeUri(string sourceUri, string targetUri)
    {
        var sourceDir = Path.GetDirectoryName(sourceUri)?.Replace('\\', '/') ?? "/";
        if (!sourceDir.EndsWith('/')) sourceDir += '/';

        if (targetUri.StartsWith(sourceDir, StringComparison.OrdinalIgnoreCase))
            return targetUri[sourceDir.Length..];

        // Walk up — remove empty entries so the trailing '/' doesn't add a phantom level
        var sourceParts = sourceDir.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var targetParts = targetUri.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var common = 0;

        while (common < sourceParts.Length &&
               common < targetParts.Length &&
               sourceParts[common].Equals(targetParts[common], StringComparison.OrdinalIgnoreCase))
            common++;

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
