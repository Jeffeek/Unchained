using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class DocumentMetadataTests
{
    [Fact]
    public void Empty_AllFieldsNull()
    {
        var m = DocumentMetadata.Empty;
        m.Title.ShouldBeNull();
        m.Author.ShouldBeNull();
        m.Subject.ShouldBeNull();
        m.Keywords.ShouldBeNull();
        m.Creator.ShouldBeNull();
        m.Producer.ShouldBeNull();
        m.CreationDate.ShouldBeNull();
        m.ModificationDate.ShouldBeNull();
    }

    [Fact]
    public void Empty_IsSingleton() =>
        DocumentMetadata.Empty.ShouldBeSameAs(DocumentMetadata.Empty);

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new DocumentMetadata(
            "T",
            "A",
            null,
            null,
            null,
            null,
            null,
            null
        );
        var b = new DocumentMetadata(
            "T",
            "A",
            null,
            null,
            null,
            null,
            null,
            null
        );
        a.ShouldBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_NotEqual()
    {
        var a = new DocumentMetadata(
            "T1",
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        var b = new DocumentMetadata(
            "T2",
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = DocumentMetadata.Empty;
        var modified = original with { Title = "My Doc" };
        modified.Title.ShouldBe("My Doc");
        original.Title.ShouldBeNull();
    }
}
