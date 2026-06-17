using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Document;

/// <summary>
///     Unit tests for <see cref="PdfDocumentCore" /> — the internal document model. Exercises
///     <see cref="PdfDocumentCore.Repair" />, linearization detection, the object cache,
///     compressed object streams, page-tree traversal, and indirect-object resolution.
/// </summary>
public sealed class PdfDocumentCoreTests
{
    [Fact]
    public void Parse_SinglePage_HasOnePage()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        core.PageCount.ShouldBe(1);
        core.IsEncrypted.ShouldBeFalse();
    }

    [Fact]
    public void Parse_MultiPage_PageCountMatches()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.MultiPage(4));
        core.PageCount.ShouldBe(4);
    }

    [Fact]
    public void GetPage_ValidNumber_ReturnsPageDictionary()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.MultiPage(3));
        var page = core.GetPage(2);
        page.GetName("Type").ShouldBe("Page");
    }

    [
        Theory,
        InlineData(0),
        InlineData(-1),
        InlineData(99)
    ]
    public void GetPage_OutOfRange_Throws(int pageNumber)
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.MultiPage(3));
        Should.Throw<ArgumentOutOfRangeException>(() => core.GetPage(pageNumber));
    }

    [Fact]
    public void IsLinearized_PlainPdf_False()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        core.IsLinearized.ShouldBeFalse();
    }

    [Fact]
    public void Catalog_ResolvesToCatalogDictionary()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        core.Catalog.GetName("Type").ShouldBe("Catalog");
    }

    [Fact]
    public void Info_WhenAbsent_ReturnsNull()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        core.Info.ShouldBeNull();
    }

    [Fact]
    public void Info_WhenPresent_ReturnsDictionary()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.WithInfo("Title", "Author"));
        core.Info.ShouldNotBeNull();
    }

    [Fact]
    public void ResolveIndirect_CachesObject()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        var first = core.ResolveIndirect(1);
        var second = core.ResolveIndirect(1);
        // Cached instance must be returned (reference equality).
        ReferenceEquals(first, second).ShouldBeTrue();
    }

    [Fact]
    public void Dereference_IndirectReference_ResolvesValue()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        // The catalog's /Pages is an indirect reference; dereference resolves it.
        var pagesRef = core.Catalog["Pages"];
        pagesRef.ShouldBeOfType<PdfIndirectReference>();
        var pages = core.Dereference(pagesRef!);
        pages.ShouldBeOfType<PdfDictionary>();
    }

    [Fact]
    public void Dereference_DirectObject_ReturnsUnchanged()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        var direct = new PdfInteger(42);
        core.Dereference(direct).ShouldBeSameAs(direct);
    }

    [Fact]
    public void TrimCache_ThenResolveAgain_StillWorks()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.MultiPage(2));
        _ = core.ResolveIndirect(1);
        core.TrimCache();
        // After cache trim, resolution must reparse without error.
        core.PageCount.ShouldBe(2);
    }

    [Fact]
    public void CollectObjects_ReturnsAllInUseObjects()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.MultiPage(2));
        var objs = core.CollectObjects();
        // Catalog + Pages + 2 page nodes = at least 4 objects.
        objs.Count.ShouldBeGreaterThanOrEqualTo(4);
        objs.ShouldBeInOrder(SortDirection.Ascending, Comparer<PdfIndirectObject>.Create(static (a, b) => a.ObjectNumber.CompareTo(b.ObjectNumber)));
    }

    [Fact]
    public void CompressedXref_ResolvesObjectStream()
    {
        // A PDF using a compressed /XRef stream must still resolve its pages.
        using var core = PdfDocumentCore.Parse(PdfFixtures.WithCompressedXref(2));
        core.PageCount.ShouldBe(2);
    }

    [Fact]
    public void Repair_RecoversPageCount()
    {
        // Build a valid PDF, then repair it (scans object headers, rebuilds xref).
        var bytes = PdfFixtures.MultiPage(3);
        using var core = PdfDocumentCore.Repair(bytes);
        core.PageCount.ShouldBe(3);
    }

    [Fact]
    public void Repair_EmptyBytes_Throws() =>
        Should.Throw<PdfException>(() => PdfDocumentCore.Repair("not a pdf at all"u8.ToArray()));

    [Fact]
    public void ResolveIndirect_FreeObject_Throws()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        // Object 0 is the free-list head — resolving it must throw.
        Should.Throw<PdfException>(() => core.ResolveIndirect(0));
    }
}
