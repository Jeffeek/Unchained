using System.Xml.Linq;
using Unchained.Pptx.Slides;

namespace Unchained.Pptx.Parsing;

/// <summary>
///     Parses PowerPoint 2010 section data from the <c>&lt;p:extLst&gt;</c> element in
///     <c>presentation.xml</c> into a <see cref="SectionCollection" />.
/// </summary>
internal static class SectionParser
{
    private const string SectionExtUri = "{521415D9-36F7-43E2-AB2F-B90AF26B5E84}";
    // PowerPoint 2010 extensions
    private static readonly XNamespace P14 =
        "http://schemas.microsoft.com/office/powerpoint/2010/main";

    /// <summary>
    ///     Scans the <c>&lt;p:extLst&gt;</c> children of <paramref name="presentationRoot" />
    ///     for the section list extension and populates <paramref name="sections" />.
    /// </summary>
    public static void Parse(XElement presentationRoot, SectionCollection sections)
    {
        var pml = XNamespace.Get(
            "http://schemas.openxmlformats.org/presentationml/2006/main");

        var extLst = presentationRoot.Element(pml + "extLst");
        if (extLst == null) return;

        foreach (var ext in extLst.Elements(pml + "ext"))
        {
            var uri = (string?)ext.Attribute("uri");
            if (!SectionExtUri.Equals(uri, StringComparison.OrdinalIgnoreCase)) continue;

            var sectionLst = ext.Element(P14 + "sectionLst");
            if (sectionLst == null) continue;

            foreach (var sec in sectionLst.Elements(P14 + "section"))
            {
                var name = (string?)sec.Attribute("name") ?? string.Empty;
                var section = new PptxSection(name);

                var sldIdLst = sec.Element(P14 + "sldIdLst");
                if (sldIdLst != null)
                {
                    foreach (var sldId in sldIdLst.Elements(P14 + "sldId"))
                    {
                        var idRaw = (string?)sldId.Attribute("id");
                        if (uint.TryParse(idRaw, out var id))
                            section.SlideIds.Add(id);
                    }
                }

                sections.AddParsed(section);
            }

            break; // only one section list extension expected
        }
    }
}
