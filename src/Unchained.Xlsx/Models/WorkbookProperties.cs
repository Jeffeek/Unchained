using Unchained.Ooxml.Properties;

namespace Unchained.Xlsx.Models;

/// <summary>
///     Metadata properties of a workbook, drawn from the OPC core properties
///     (<c>docProps/core.xml</c>) and the extended app properties (<c>docProps/app.xml</c>).
/// </summary>
public sealed class WorkbookProperties : OoXmlCoreProperties;
