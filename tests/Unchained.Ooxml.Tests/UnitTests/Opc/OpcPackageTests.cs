using Shouldly;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Opc;

public sealed class OpcPackageTests
{
    [Fact]
    public void CreateEmpty_HasNoParts()
    {
        using var package = OpcPackage.CreateEmpty();
        package.Parts.ShouldBeEmpty();
    }

    [Fact]
    public void AddOrReplacePart_ThenGetPart_ReturnsSamePart()
    {
        using var package = OpcPackage.CreateEmpty();
        var data = new byte[] { 1, 2, 3 };
        package.AddOrReplacePart("/test/part.xml", OoxmlContentTypes.ApplicationXml, data);

        var part = package.GetPart("/test/part.xml");
        part.ShouldNotBeNull();
        part.Data.ShouldBe(data);
        part.ContentType.ShouldBe(OoxmlContentTypes.ApplicationXml);
    }

    [Fact]
    public void GetPart_NonExistentUri_ThrowsOoXmlException()
    {
        using var package = OpcPackage.CreateEmpty();
        Should.Throw<OoXmlException>(() => package.GetPart("/missing.xml"));
    }

    [Fact]
    public void TryGetPart_NonExistentUri_ReturnsNull()
    {
        using var package = OpcPackage.CreateEmpty();
        package.TryGetPart("/missing.xml").ShouldBeNull();
    }

    [Fact]
    public void AddOrReplacePart_LeadingSlashNormalized()
    {
        using var package = OpcPackage.CreateEmpty();
        package.AddOrReplacePart("test/part.xml", OoxmlContentTypes.ApplicationXml, []);
        package.TryGetPart("/test/part.xml").ShouldNotBeNull();
    }

    [Fact]
    public void SaveAndReopen_PreservesPart()
    {
        using var package = OpcPackage.CreateEmpty();
        var data = "<root/>"u8.ToArray();
        package.AddOrReplacePart("/doc/doc.xml", OoxmlContentTypes.ApplicationXml, data);
        package.AddPackageRelationship("rId1", "http://example.com/rel", "doc/doc.xml");

        var bytes = package.Save();
        bytes.ShouldNotBeEmpty();

        using var reopened = OpcPackage.Open(bytes);
        var part = reopened.TryGetPart("/doc/doc.xml");
        part.ShouldNotBeNull();
        part.Data.ShouldBe(data);
    }

    [Fact]
    public void Relationships_RoundTrip()
    {
        using var package = OpcPackage.CreateEmpty();
        package.AddOrReplacePart("/ppt/slide1.xml", OoxmlContentTypes.ApplicationXml, []);
        package.AddOrReplacePart("/ppt/slideLayout1.xml", OoxmlContentTypes.ApplicationXml, []);
        package.AddRelationship(
            "/ppt/slide1.xml",
            "rId1",
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout",
            "../slideLayouts/slideLayout1.xml"
        );

        var bytes = package.Save();
        using var reopened = OpcPackage.Open(bytes);
        var rels = reopened.GetRelationships("/ppt/slide1.xml");
        rels.Count.ShouldBe(1);
        rels[0].Id.ShouldBe("rId1");
    }
}
