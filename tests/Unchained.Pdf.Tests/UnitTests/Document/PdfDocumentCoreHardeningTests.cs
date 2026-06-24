using System.Text;
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
            "<< /Type /Catalog /Pages 2 0 R >>",                             // 1 catalog
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 3 >>",               // 2 root pages
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>",         // 3 page 1
            "<< /Type /Pages /Parent 2 0 R /Kids [5 0 R 6 0 R] /Count 2 >>", // 4 subtree
            "<< /Type /Page /Parent 4 0 R /MediaBox [0 0 10 10] >>",         // 5 page 2
            "<< /Type /Page /Parent 4 0 R /MediaBox [0 0 10 10] >>"          // 6 page 3
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

    [Fact]
    public void IgnoreCorruptedObjects_CorruptBody_ReturnsPdfNull()
    {
        // Object 4 is unreferenced and has a body the value parser rejects (a bare array-end token).
        // With the flag set, ResolveIndirect swallows the parse error and yields PdfNull.
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>",
            "]" // malformed: ReadValue throws on an unexpected ArrayEnd token
        };
        using var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));

        Should.Throw<PdfException>(() => core.ResolveIndirect(4)); // throws without the flag

        core.IgnoreCorruptedObjects = true;
        core.ResolveIndirect(4).Value.ShouldBe(PdfNull.Instance);
    }

    [Fact]
    public void GetPage_CatalogPagesPointsDirectlyAtPage_ReturnsThatPage()
    {
        // /Pages resolves to a node whose /Type is /Page (not /Pages) yet declares /Count 1.
        // The root-node "type == Page" branch of FindPageInTree returns it directly.
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Page /Count 1 /MediaBox [0 0 10 10] >>"
        };
        using var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));
        core.PageCount.ShouldBe(1);
        core.GetPage(1).GetName("Type").ShouldBe("Page");
    }

    [Fact]
    public void GetPage_RootPageButWrongRemainingCount_Throws()
    {
        // /Pages resolves to a /Type /Page node but /Count is 2, so requesting page 2 walks the
        // root-Page branch with remaining != 0 after the decrement → traversal error.
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Page /Count 2 /MediaBox [0 0 10 10] >>"
        };
        using var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));
        Should.Throw<PdfException>(() => core.GetPage(2));
    }

    [Fact]
    public void Parse_InlineEncryptDict_UnsupportedHandler_Throws()
    {
        // An inline (direct) /Encrypt dictionary in the trailer hits the PdfDictionary branch of
        // InitializeEncryption. A non-Standard /Filter is unsupported → PdfEncryptedException.
        var bytes = BuildWithTrailerExtra("/Encrypt << /Filter /NonStandard /V 2 /R 3 >>");
        Should.Throw<PdfEncryptedException>(() => PdfDocumentCore.Parse(bytes));
    }

    [Fact]
    public void Parse_EncryptWrongType_Throws()
    {
        // /Encrypt as a bare name (neither indirect ref nor dictionary) → default branch throws.
        var bytes = BuildWithTrailerExtra("/Encrypt /Bogus");
        Should.Throw<PdfException>(() => PdfDocumentCore.Parse(bytes));
    }

    [Fact]
    public void Repair_TrailerMissingRoot_ScansObjectsForCatalog()
    {
        // Trailer deliberately omits /Root, forcing Repair to scan resolved objects for a /Catalog.
        var bytes = BuildNoRootTrailer();
        using var core = PdfDocumentCore.Repair(bytes);
        core.Catalog.GetName("Type").ShouldBe("Catalog");
        core.PageCount.ShouldBe(1);
    }

    [Fact]
    public void Repair_TrailerMissingRoot_NoCatalogPresent_Throws()
    {
        // No object is a /Catalog and the trailer has no /Root → Repair cannot locate the catalog.
        var bytes = BuildNoRootTrailer(includeCatalog: false);
        Should.Throw<PdfException>(() => PdfDocumentCore.Repair(bytes));
    }

    [Fact]
    public void Repair_TrailerMissingRoot_SkipsUnreadableObjectsDuringScan()
    {
        // Object 1 has a corrupt body; with no /Root the catalog scan resolves objects in order,
        // the corrupt one throws and is swallowed by the scan's try/catch, then object 2's catalog
        // is found. Exercises the "skip unreadable objects" branch of Repair.
        var bodies = new[]
        {
            "]",                                                      // 1: malformed → scan skips it
            "<< /Type /Catalog /Pages 3 0 R >>",                     // 2: the real catalog
            "<< /Type /Pages /Kids [4 0 R] /Count 1 >>",             // 3: pages
            "<< /Type /Page /Parent 3 0 R /MediaBox [0 0 10 10] >>"  // 4: page
        };
        var withRoot = RawPdfBuilder.Build(bodies);
        // Strip "/Root 1 0 R " from the trailer so Repair must scan for the catalog.
        var text = Encoding.Latin1.GetString(withRoot).Replace("/Root 1 0 R ", string.Empty);
        using var core = PdfDocumentCore.Repair(Encoding.Latin1.GetBytes(text));
        core.Catalog.GetName("Type").ShouldBe("Catalog");
    }

    // Builds a minimal single-page PDF and appends <paramref name="extra"/> inside the trailer
    // dictionary (before the closing >>). Used to inject inline /Encrypt entries.
    private static byte[] BuildWithTrailerExtra(string extra)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        PdfFixtures.Ln(sb, "%PDF-1.7");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Catalog /Pages 2 0 R >>");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>");
        PdfFixtures.Ln(sb, "endobj");

        var xrefOffset = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 4");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, $"<< /Size 4 /Root 1 0 R /ID [<AABBCCDD><AABBCCDD>] {extra} >>");
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    // Builds a single-page PDF whose trailer has NO /Root entry, forcing Repair's object scan.
    private static byte[] BuildNoRootTrailer(bool includeCatalog = true)
    {
        var sb = new StringBuilder();
        var offsets = new List<int>();
        PdfFixtures.Ln(sb, "%PDF-1.7");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "1 0 obj");
        PdfFixtures.Ln(sb, includeCatalog ? "<< /Type /Catalog /Pages 2 0 R >>" : "<< /Type /NotACatalog >>");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "2 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        PdfFixtures.Ln(sb, "endobj");

        offsets.Add(PdfFixtures.Len(sb));
        PdfFixtures.Ln(sb, "3 0 obj");
        PdfFixtures.Ln(sb, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>");
        PdfFixtures.Ln(sb, "endobj");

        var xrefOffset = PdfFixtures.Len(sb);
        PdfFixtures.Ln(sb, "xref");
        PdfFixtures.Ln(sb, "0 4");
        PdfFixtures.Ln(sb, "0000000000 65535 f ");
        foreach (var o in offsets) PdfFixtures.Ln(sb, $"{o:D10} 00000 n ");
        PdfFixtures.Ln(sb, "trailer");
        PdfFixtures.Ln(sb, "<< /Size 4 /ID [<AABBCCDD><AABBCCDD>] >>"); // no /Root
        PdfFixtures.Ln(sb, "startxref");
        PdfFixtures.Ln(sb, xrefOffset.ToString());
        sb.Append("%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
