using Shouldly;
using Unchained.Ooxml.Opc;
using Unchained.Ooxml.Xml;
using Xunit;

namespace Unchained.Ooxml.Tests.UnitTests.Opc;

/// <summary>
///     Tests for <see cref="OpcPart" /> URI resolution and relationship lookup — the relative/absolute
///     target handling and <c>..</c>/<c>.</c> path normalisation used when following relationships.
/// </summary>
public sealed class OpcPartTests
{
    private static OpcPart Part(string uri) => new(uri, OoxmlContentTypes.ApplicationXml, [1, 2, 3]);

    [Fact]
    public void ResolveUri_AbsoluteTarget_ReturnedAsIs() =>
        Part("/ppt/presentation.xml").ResolveUri("/ppt/slides/slide1.xml").ShouldBe("/ppt/slides/slide1.xml");

    [Fact]
    public void ResolveUri_RelativeTarget_CombinesWithBaseDirectory() =>
        Part("/ppt/presentation.xml").ResolveUri("slides/slide1.xml").ShouldBe("/ppt/slides/slide1.xml");

    [Fact]
    public void ResolveUri_ParentTraversal_IsNormalised() =>
        Part("/ppt/slides/slide1.xml").ResolveUri("../media/image1.png").ShouldBe("/ppt/media/image1.png");

    [Fact]
    public void ResolveUri_CurrentDirectorySegments_AreDropped() =>
        Part("/ppt/slides/slide1.xml").ResolveUri("./notesSlides/notes1.xml").ShouldBe("/ppt/slides/notesSlides/notes1.xml");

    [Fact]
    public void ResolveUri_ExcessParentTraversal_StopsAtRoot() =>
        Part("/a.xml").ResolveUri("../../../b.xml").ShouldBe("/b.xml");

    [Fact]
    public void FindRelationship_NoMatch_ReturnsNull() =>
        Part("/ppt/presentation.xml").FindRelationship("http://example/none").ShouldBeNull();

    [Fact]
    public void AddRelationship_ThenFind_ReturnsIt()
    {
        var part = Part("/ppt/presentation.xml");
        const string type = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide";
        // ReSharper disable once RedundantArgumentDefaultValue
        part.AddRelationship(new OpcRelationship("rId1", type, "slides/slide1.xml", false));

        part.FindRelationship(type).ShouldNotBeNull();
        part.FindRelationships(type).Count.ShouldBe(1);
        part.Relationships.Count.ShouldBe(1);
    }

    [Fact]
    public void FindRelationships_MultipleOfSameType_ReturnsAll()
    {
        var part = Part("/ppt/presentation.xml");
        const string type = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide";
        // ReSharper disable RedundantArgumentDefaultValue
        part.AddRelationship(new OpcRelationship("rId1", type, "slides/slide1.xml", false));
        part.AddRelationship(new OpcRelationship("rId2", type, "slides/slide2.xml", false));
        // ReSharper restore RedundantArgumentDefaultValue

        part.FindRelationships(type).Count.ShouldBe(2);
    }
}
