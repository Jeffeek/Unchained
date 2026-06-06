using Unchained.Ooxml;
using Unchained.Pptx.Core;
using Unchained.Pptx.Core.Xml;
using System.Xml.Linq;

namespace Unchained.Pptx.Core.Xml;

/// <summary>
/// PresentationML XML namespace constants and commonly-used element/attribute names.
/// All values are taken directly from ECMA-376 5th Edition.
/// </summary>
internal static class PmlNames
{
    // ── Namespaces ───────────────────────────────────────────────────────────

    /// <summary>PresentationML main namespace: <c>http://schemas.openxmlformats.org/presentationml/2006/main</c></summary>
    public static readonly XNamespace Pml = "http://schemas.openxmlformats.org/presentationml/2006/main";

    /// <summary>Relationship namespace: <c>http://schemas.openxmlformats.org/officeDocument/2006/relationships</c></summary>
    public static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>Markup compatibility namespace.</summary>
    public static readonly XNamespace MarkupCompatibility = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    // ── Relationship type URIs ────────────────────────────────────────────────

    private const string RelBase = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    private const string PkgRelBase = "http://schemas.openxmlformats.org/package/2006/relationships/";

    /// <summary>Relationship type for the presentation part.</summary>
    public const string RelTypePresentation = RelBase + "officeDocument";

    /// <summary>Relationship type for a slide part.</summary>
    public const string RelTypeSlide = RelBase + "slide";

    /// <summary>Relationship type for a slide layout part.</summary>
    public const string RelTypeSlideLayout = RelBase + "slideLayout";

    /// <summary>Relationship type for a slide master part.</summary>
    public const string RelTypeSlideMaster = RelBase + "slideMaster";

    /// <summary>Relationship type for a theme part.</summary>
    public const string RelTypeTheme = RelBase + "theme";

    /// <summary>Relationship type for a notes slide part.</summary>
    public const string RelTypeNotesSlide = RelBase + "notesSlide";

    /// <summary>Relationship type for a notes master part.</summary>
    public const string RelTypeNotesMaster = RelBase + "notesMaster";

    /// <summary>Relationship type for an image.</summary>
    public const string RelTypeImage = RelBase + "image";

    /// <summary>Relationship type for an audio file.</summary>
    public const string RelTypeAudio = RelBase + "audio";

    /// <summary>Relationship type for a video file.</summary>
    public const string RelTypeVideo = RelBase + "video";

    /// <summary>Relationship type for a chart part.</summary>
    public const string RelTypeChart = RelBase + "chart";

    /// <summary>Relationship type for a slide comment part.</summary>
    public const string RelTypeComments = RelBase + "comments";

    /// <summary>Relationship type for the presentation-level comment authors part.</summary>
    public const string RelTypeCommentAuthors = RelBase + "commentAuthors";

    /// <summary>Relationship type for a hyperlink.</summary>
    public const string RelTypeHyperlink = RelBase + "hyperlink";

    /// <summary>Relationship type for core properties (package level).</summary>
    public const string RelTypeCoreProperties = PkgRelBase + "metadata/core-properties";

    /// <summary>Relationship type for extended (app) properties.</summary>
    public const string RelTypeExtendedProperties = RelBase + "extended-properties";

    // ── Content types ─────────────────────────────────────────────────────────

    /// <summary>Content type for <c>presentation.xml</c>.</summary>
    public const string ContentTypePresentation =
        "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml";

    /// <summary>Content type for a slide part.</summary>
    public const string ContentTypeSlide =
        "application/vnd.openxmlformats-officedocument.presentationml.slide+xml";

    /// <summary>Content type for a slide layout part.</summary>
    public const string ContentTypeSlideLayout =
        "application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml";

    /// <summary>Content type for a slide master part.</summary>
    public const string ContentTypeSlideMaster =
        "application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml";

    /// <summary>Content type for a theme part.</summary>
    public const string ContentTypeTheme =
        "application/vnd.openxmlformats-officedocument.theme+xml";

    /// <summary>Content type for a notes slide part.</summary>
    public const string ContentTypeNotesSlide =
        "application/vnd.openxmlformats-officedocument.presentationml.notesSlide+xml";

    /// <summary>Content type for a notes master part.</summary>
    public const string ContentTypeNotesMaster =
        "application/vnd.openxmlformats-officedocument.presentationml.notesMaster+xml";

    /// <summary>Content type for core properties.</summary>
    public const string ContentTypeCoreProperties =
        "application/vnd.openxmlformats-package.core-properties+xml";

    /// <summary>Content type for extended (app) properties.</summary>
    public const string ContentTypeExtendedProperties =
        "application/vnd.openxmlformats-officedocument.extended-properties+xml";

    /// <summary>Content type for a chart part.</summary>
    public const string ContentTypeChart =
        "application/vnd.openxmlformats-officedocument.drawingml.chart+xml";

    /// <summary>Content type for a slide comment part.</summary>
    public const string ContentTypeComments =
        "application/vnd.openxmlformats-officedocument.presentationml.comments+xml";

    /// <summary>Content type for the comment authors part.</summary>
    public const string ContentTypeCommentAuthors =
        "application/vnd.openxmlformats-officedocument.presentationml.commentAuthors+xml";

    // ── PresentationML element names ──────────────────────────────────────────

    /// <summary><c>&lt;p:presentation&gt;</c> — root element of the presentation part.</summary>
    public static readonly XName Presentation = Pml + "presentation";

    /// <summary><c>&lt;p:sldMasterIdLst&gt;</c> — list of slide master IDs.</summary>
    public static readonly XName SlideMasterIdList = Pml + "sldMasterIdLst";

    /// <summary><c>&lt;p:sldMasterId&gt;</c> — one slide master entry.</summary>
    public static readonly XName SlideMasterId = Pml + "sldMasterId";

    /// <summary><c>&lt;p:sldIdLst&gt;</c> — ordered list of slide IDs.</summary>
    public static readonly XName SlideIdList = Pml + "sldIdLst";

    /// <summary><c>&lt;p:sldId&gt;</c> — one slide entry.</summary>
    public static readonly XName SlideId = Pml + "sldId";

    /// <summary><c>&lt;p:sldSz&gt;</c> — slide size (cx/cy in EMU).</summary>
    public static readonly XName SlideSize = Pml + "sldSz";

    /// <summary><c>&lt;p:notesSz&gt;</c> — notes slide size.</summary>
    public static readonly XName NotesSize = Pml + "notesSz";

    /// <summary><c>&lt;p:sld&gt;</c> — root element of a slide part.</summary>
    public static readonly XName Slide = Pml + "sld";

    /// <summary><c>&lt;p:cSld&gt;</c> — common slide data.</summary>
    public static readonly XName CommonSlideData = Pml + "cSld";

    /// <summary><c>&lt;p:spTree&gt;</c> — shape tree (container for all shapes on a slide).</summary>
    public static readonly XName ShapeTree = Pml + "spTree";

    /// <summary><c>&lt;p:sp&gt;</c> — shape (AutoShape or TextBox).</summary>
    public static readonly XName Shape = Pml + "sp";

    /// <summary><c>&lt;p:pic&gt;</c> — picture shape.</summary>
    public static readonly XName Picture = Pml + "pic";

    /// <summary><c>&lt;p:graphicFrame&gt;</c> — graphic frame (contains charts, tables, diagrams).</summary>
    public static readonly XName GraphicFrame = Pml + "graphicFrame";

    /// <summary><c>&lt;p:grpSp&gt;</c> — group shape.</summary>
    public static readonly XName GroupShape = Pml + "grpSp";

    /// <summary><c>&lt;p:cxnSp&gt;</c> — connector shape.</summary>
    public static readonly XName Connector = Pml + "cxnSp";

    /// <summary><c>&lt;p:nvSpPr&gt;</c> — non-visual shape properties.</summary>
    public static readonly XName NonVisualShapeProperties = Pml + "nvSpPr";

    /// <summary><c>&lt;p:nvPicPr&gt;</c> — non-visual picture properties.</summary>
    public static readonly XName NonVisualPictureProperties = Pml + "nvPicPr";

    /// <summary><c>&lt;p:nvCxnSpPr&gt;</c> — non-visual connector properties.</summary>
    public static readonly XName NonVisualConnectorProperties = Pml + "nvCxnSpPr";

    /// <summary><c>&lt;p:nvGrpSpPr&gt;</c> — non-visual group shape properties.</summary>
    public static readonly XName NonVisualGroupShapeProperties = Pml + "nvGrpSpPr";

    /// <summary><c>&lt;p:nvGraphicFramePr&gt;</c> — non-visual graphic frame properties.</summary>
    public static readonly XName NonVisualGraphicFrameProperties = Pml + "nvGraphicFramePr";

    /// <summary><c>&lt;p:cNvPr&gt;</c> — common non-visual properties (id, name, description).</summary>
    public static readonly XName CommonNonVisualProperties = Pml + "cNvPr";

    /// <summary><c>&lt;p:spPr&gt;</c> — shape properties (geometry, position, fill, line).</summary>
    public static readonly XName ShapeProperties = Pml + "spPr";

    /// <summary><c>&lt;p:txBody&gt;</c> — text body inside a shape.</summary>
    public static readonly XName TextBody = Pml + "txBody";

    /// <summary><c>&lt;p:bg&gt;</c> — slide background.</summary>
    public static readonly XName Background = Pml + "bg";

    /// <summary><c>&lt;p:bgPr&gt;</c> — slide background properties.</summary>
    public static readonly XName BackgroundProperties = Pml + "bgPr";

    /// <summary><c>&lt;p:transition&gt;</c> — slide transition.</summary>
    public static readonly XName Transition = Pml + "transition";

    /// <summary><c>&lt;p:timing&gt;</c> — animation timing.</summary>
    public static readonly XName Timing = Pml + "timing";

    /// <summary><c>&lt;p:clrMapOvr&gt;</c> — colour map override.</summary>
    public static readonly XName ColorMapOverride = Pml + "clrMapOvr";

    /// <summary><c>&lt;p:sldMaster&gt;</c> — root element of a slide master part.</summary>
    public static readonly XName SlideMaster = Pml + "sldMaster";

    /// <summary><c>&lt;p:sldLayout&gt;</c> — root element of a slide layout part.</summary>
    public static readonly XName SlideLayout = Pml + "sldLayout";

    /// <summary><c>&lt;p:notes&gt;</c> — root element of a notes slide part.</summary>
    public static readonly XName Notes = Pml + "notes";

    /// <summary><c>&lt;p:sldLayoutIdLst&gt;</c> — list of layout IDs on a master.</summary>
    public static readonly XName SlideLayoutIdList = Pml + "sldLayoutIdLst";

    /// <summary><c>&lt;p:sldLayoutId&gt;</c> — one layout entry on a master.</summary>
    public static readonly XName SlideLayoutId = Pml + "sldLayoutId";

    /// <summary><c>&lt;p:blipFill&gt;</c> — picture fill referencing an image relationship.</summary>
    public static readonly XName BlipFill = Pml + "blipFill";

    /// <summary><c>&lt;p:grpSpPr&gt;</c> — group shape properties.</summary>
    public static readonly XName GroupShapeProperties = Pml + "grpSpPr";

    /// <summary><c>&lt;p:nvPr&gt;</c> — application-specific non-visual properties.</summary>
    public static readonly XName ApplicationNonVisualProperties = Pml + "nvPr";

    // ── Common PresentationML attribute names ─────────────────────────────────

    /// <summary>Relationship reference attribute: <c>r:id</c></summary>
    public static readonly XName RelationshipId = Relationships + "id";

    /// <summary>Slide identifier attribute: <c>id</c></summary>
    public const string AttributeId = "id";

    /// <summary>Slide name attribute: <c>name</c></summary>
    public const string AttributeName = "name";

    /// <summary>Hidden slide attribute: <c>show</c> (0 = hidden).</summary>
    public const string AttributeShow = "show";

    /// <summary>Width in EMU: <c>cx</c></summary>
    public const string AttributeWidth = "cx";

    /// <summary>Height in EMU: <c>cy</c></summary>
    public const string AttributeHeight = "cy";
}
