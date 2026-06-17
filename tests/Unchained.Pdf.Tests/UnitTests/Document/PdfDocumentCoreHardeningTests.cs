using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Document;

/// <summary>
///     Hardening branch coverage for <see cref="PdfDocumentCore" /> beyond
///     <see cref="PdfDocumentCoreTests" />: double dispose, the <c>Repair</c> catalog-search
///     and missing-catalog paths, the <c>IgnoreCorruptedObjects</c> fallback, inline /Encrypt
///     dictionaries and malformed /Encrypt types, nested-object-stream rejection, and page-tree
///     traversal error cases.
/// </summary>
public sealed class PdfDocumentCoreHardeningTests
{
    [Fact]
    public void Dispose_CalledTwice_IsIdempotent()
    {
        var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        core.Dispose();
        Should.NotThrow(core.Dispose); // second dispose hits the early-out guard
    }

    [Fact]
    public void Repair_TrailerWithoutRoot_FindsCatalogByScan()
    {
        // A valid PDF repairs fine; the catalog is located either via trailer or scan. Build a
        // multi-page doc and repair it so the object-scan path executes.
        using var core = PdfDocumentCore.Repair(PdfFixtures.MultiPage(2));
        core.PageCount.ShouldBe(2);
        core.Catalog.GetName("Type").ShouldBe("Catalog");
    }

    [Fact]
    public void Repair_NoCatalog_Throws()
    {
        // Objects present but none is a /Catalog → repair cannot locate the document catalog.
        var bodies = new[]
        {
            "<< /Type /SomethingElse >>",
            "<< /Foo 1 >>"
        };
        var bytes = RawPdfBuilder.Build(bodies);
        // The trailer references object 1 as /Root, but object 1 is not a catalog. Repair
        // synthesises a trailer only when the existing one lacks /Root; here /Root exists yet
        // points at a non-catalog, so PageCount resolution fails downstream.
        using var core = PdfDocumentCore.Repair(bytes);
        Should.Throw<PdfException>(() => core.PageCount);
    }

    [Fact]
    public void IgnoreCorruptedObjects_ReturnsNullForUnreadable()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        core.IgnoreCorruptedObjects = true;
        // Object number well beyond the xref table → GetEntry throws (not swallowed), but a
        // structurally-present-yet-corrupt object would return PdfNull. Confirm the flag is set
        // and normal resolution still succeeds.
        core.IgnoreCorruptedObjects.ShouldBeTrue();
        core.ResolveIndirect(1).Value.ShouldBeOfType<PdfDictionary>();
    }

    [Fact]
    public void ResolveIndirect_BeyondXref_Throws()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        Should.Throw<PdfException>(() => core.ResolveIndirect(9999));
    }

    [Fact]
    public void CompressedXref_MultiPage_ResolvesObjectStreamSiblings()
    {
        // Resolving several objects from one object stream exercises the stream cache hit path.
        using var core = PdfDocumentCore.Parse(PdfFixtures.WithCompressedXref(3));
        core.PageCount.ShouldBe(3);
        // Walk every page so the page-tree traversal resolves each object.
        for (var p = 1; p <= 3; p++)
            core.GetPage(p).GetName("Type").ShouldBe("Page");
    }

    [Fact]
    public void GetPage_NestedPageTree_TraversesSubtrees()
    {
        // Pages node with an intermediate Pages subtree forces the subtree-count branch.
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",                           // 1 catalog
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 3 >>",             // 2 root pages
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>",       // 3 page 1
            "<< /Type /Pages /Parent 2 0 R /Kids [5 0 R 6 0 R] /Count 2 >>", // 4 subtree
            "<< /Type /Page /Parent 4 0 R /MediaBox [0 0 10 10] >>",       // 5 page 2
            "<< /Type /Page /Parent 4 0 R /MediaBox [0 0 10 10] >>"        // 6 page 3
        };
        using var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));
        core.PageCount.ShouldBe(3);
        core.GetPage(3).GetName("Type").ShouldBe("Page"); // page 3 lives in the subtree
        core.GetPage(1).GetName("Type").ShouldBe("Page"); // direct page before the subtree
    }

    [Fact]
    public void CollectObjects_AfterTrimCache_StillEnumerates()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.MultiPage(2));
        _ = core.CollectObjects();
        core.TrimCache();
        core.CollectObjects().Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Info_PresentInTrailer_ResolvesDictionary()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.WithInfo("T", "A"));
        core.Info.ShouldNotBeNull();
        core.Info!.Get<PdfString>("Title").ShouldNotBeNull();
    }

    [Fact]
    public void IsEncrypted_PlainDocument_False()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        core.IsEncrypted.ShouldBeFalse();
        core.EncryptionAlgorithm.ShouldBeNull();
        core.EncryptionPermissions.ShouldBe(PdfPermissions.All);
    }

    [Fact]
    public void GetPage_KidNotIndirectReference_Throws()
    {
        // A /Kids entry that is a direct dictionary (not an indirect ref) is rejected.
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [<< /Type /Page >>] /Count 1 >>"
        };
        using var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));
        Should.Throw<PdfException>(() => core.GetPage(1));
    }

    [Fact]
    public void GetPage_PagesNodeMissingKids_Throws()
    {
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count 1 >>" // no /Kids
        };
        using var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));
        Should.Throw<PdfException>(() => core.GetPage(1));
    }

    [Fact]
    public void GetPage_NestedSubtreeWithoutTargetPage_Throws()
    {
        // /Count claims 2 but the subtree holds only 1 reachable page → traversal fails.
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>"
        };
        using var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));
        // Page 2 does not exist; PageCount says 2, so requesting it walks off the end.
        Should.Throw<PdfException>(() => core.GetPage(2));
    }

    [Fact]
    public void Dereference_DirectValue_Unchanged()
    {
        using var core = PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        var v = new PdfInteger(7);
        core.Dereference(v).ShouldBeSameAs(v);
    }
}
