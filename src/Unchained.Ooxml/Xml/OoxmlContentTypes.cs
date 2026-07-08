namespace Unchained.Ooxml.Xml;

/// <summary>
///     Shared OPC content-type constants used across OOXML formats.
///     Consolidated to prevent drift between Pptx, Xlsx, and other consumers.
/// </summary>
internal static class OoxmlContentTypes
{
    /// <summary>Core properties: <c>application/vnd.openxmlformats-package.core-properties+xml</c></summary>
    public const string CoreProperties = "application/vnd.openxmlformats-package.core-properties+xml";

    /// <summary>Extended properties: <c>application/vnd.openxmlformats-officedocument.extended-properties+xml</c></summary>
    public const string ExtendedProperties = "application/vnd.openxmlformats-officedocument.extended-properties+xml";

    /// <summary>Theme: <c>application/vnd.openxmlformats-officedocument.theme+xml</c></summary>
    public const string Theme = "application/vnd.openxmlformats-officedocument.theme+xml";

    /// <summary>Chart: <c>application/vnd.openxmlformats-officedocument.drawingml.chart+xml</c></summary>
    public const string Chart = "application/vnd.openxmlformats-officedocument.drawingml.chart+xml";

    /// <summary>Generic XML content type.</summary>
    public const string ApplicationXml = "application/xml";
}
