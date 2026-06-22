using System.Text;
using System.Xml.Linq;
using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Direct unit tests for <see cref="XmpDocumentHelper" /> — reading an existing XMP packet from
///     the catalog (direct stream and missing), lenient parse failures, the minimal-packet builder,
///     and the set-or-add element primitive.
/// </summary>
public sealed class XmpDocumentHelperTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    [Fact]
    public void ReadExistingXmp_NoMetadata_ReturnsNull() =>
        XmpDocumentHelper.ReadExistingXmp(new Dictionary<string, PdfObject>(), Core()).ShouldBeNull();

    [Fact]
    public void ReadExistingXmp_DirectStream_ReturnsDecodedXml()
    {
        const string xml = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"></x:xmpmeta>";
        var stream = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject> { ["Length"] = new PdfInteger(xml.Length) }),
            Encoding.UTF8.GetBytes(xml)
        );
        var entries = new Dictionary<string, PdfObject> { ["Metadata"] = stream };

        var result = XmpDocumentHelper.ReadExistingXmp(entries, Core());
        result.ShouldNotBeNull();
        result.ShouldContain("xmpmeta");
    }

    [Fact]
    public void ReadExistingXmp_NonStreamMetadata_ReturnsNull()
    {
        var entries = new Dictionary<string, PdfObject> { ["Metadata"] = new PdfInteger(5) };
        XmpDocumentHelper.ReadExistingXmp(entries, Core()).ShouldBeNull();
    }

    [Fact]
    public void TryParse_ValidXml_ReturnsDocument()
    {
        var doc = XmpDocumentHelper.TryParse("<root><child/></root>");
        doc.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("root");
    }

    [Fact]
    public void TryParse_InvalidXml_ReturnsNull() =>
        XmpDocumentHelper.TryParse("<not valid <<<").ShouldBeNull();

    [Fact]
    public void CreateMinimalXmp_HasRdfRoot()
    {
        var doc = XmpDocumentHelper.CreateMinimalXmp();
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        doc.Descendants(rdf + "RDF").ShouldNotBeEmpty();
    }

    [Fact]
    public void SetOrAdd_NewElement_IsAdded()
    {
        var parent = new XElement("parent");
        XName name = "value";
        XmpDocumentHelper.SetOrAdd(parent, name, "hello");
        parent.Element(name)!.Value.ShouldBe("hello");
    }

    [Fact]
    public void SetOrAdd_ExistingElement_IsUpdated()
    {
        XName name = "value";
        var parent = new XElement("parent", new XElement(name, "old"));
        XmpDocumentHelper.SetOrAdd(parent, name, "new");
        parent.Elements(name).Count().ShouldBe(1);
        parent.Element(name)!.Value.ShouldBe("new");
    }
}
