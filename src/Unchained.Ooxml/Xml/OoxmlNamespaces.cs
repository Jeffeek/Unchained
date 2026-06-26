using System.Xml.Linq;

namespace Unchained.Ooxml.Xml;

/// <summary>
///     Shared XML namespace constants used across OOXML formats.
///     Consolidated to prevent drift between Pptx, Xlsx, and other consumers.
/// </summary>
internal static class OoxmlNamespaces
{
    // ── OPC package metadata namespaces ─────────────────────────────────────

    /// <summary>Package metadata base: <c>http://schemas.openxmlformats.org/package/2006/metadata</c></summary>
    public const string PackageMetadata = "http://schemas.openxmlformats.org/package/2006/metadata";

    /// <summary>Core properties rel-type: <c>http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties</c></summary>
    public const string RelCoreProperties = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties";

    /// <summary>Extended properties: <c>http://schemas.openxmlformats.org/officeDocument/2006/extended-properties</c></summary>
    public const string ExtendedProperties = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

    /// <summary>Extended properties rel-type: <c>extended-properties</c></summary>
    public const string RelExtendedProperties = "/extended-properties";

    /// <summary>Office document relationships: <c>http://schemas.openxmlformats.org/officeDocument/2006/relationships</c></summary>
    public const string OfficeDocument = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>Package relationships: <c>http://schemas.openxmlformats.org/package/2006/relationships</c></summary>
    public const string PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    // ── Dublin Core namespaces ──────────────────────────────────────────────

    /// <summary>Dublin Core elements: <c>http://purl.org/dc/elements/1.1/</c></summary>
    public const string DublinCore = "http://purl.org/dc/elements/1.1/";

    /// <summary>Dublin Core terms: <c>http://purl.org/dc/terms/</c></summary>
    public const string DublinTerms = "http://purl.org/dc/terms/";

    /// <summary>XML Schema instance: <c>http://www.w3.org/2001/XMLSchema-instance</c></summary>
    public const string XmlSchemaInstance = "http://www.w3.org/2001/XMLSchema-instance";

    // ── DrawingML namespaces ────────────────────────────────────────────────

    /// <summary>DrawingML main: <c>http://schemas.openxmlformats.org/drawingml/2006/main</c></summary>
    public const string DrawingML = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>DrawingML chart: <c>http://schemas.openxmlformats.org/drawingml/2006/chart</c></summary>
    public const string Chart = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    /// <summary>DrawingML table: <c>http://schemas.openxmlformats.org/drawingml/2006/table</c></summary>
    public const string Table = DrawingML + "/table";

    // ── Relationship-type suffixes (append to OfficeDocument or PackageRelationships) ──

    /// <summary>Image: <c>/image</c></summary>
    public const string RelImage = "/image";

    /// <summary>Chart: <c>/chart</c></summary>
    public const string RelChart = "/chart";

    /// <summary>Hyperlink: <c>/hyperlink</c></summary>
    public const string RelHyperlink = "/hyperlink";

    /// <summary>Theme: <c>/theme</c></summary>
    public const string RelTheme = "/theme";

    /// <summary>Comments: <c>/comments</c></summary>
    public const string RelComments = "/comments";

    /// <summary>Digital signature: <c>/digital-signature</c></summary>
    public const string RelDigitalSignature = "/digital-signature";
}
