using Shouldly;
using Unchained.Pdf.Engine;
using Xunit;
using Unchained.Pdf.Tests.Helpers;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class XmpMetadataTests : PdfTestBase
{
    private static readonly XmpMetadataEditor Editor = new();

    private const string SampleXmp =
        """<?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?><x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"><rdf:Description rdf:about=""><dc:title xmlns:dc="http://purl.org/dc/elements/1.1/"><rdf:Alt><rdf:li xml:lang="x-default">Test Document</rdf:li></rdf:Alt></dc:title></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end="w"?>""";


    [Fact]
    public async Task GetXmpMetadata_NoMetadata_ReturnsNull()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        doc.GetXmpMetadata().ShouldBeNull();
    }

    [Fact]
    public async Task SetXmpMetadata_CanReadBack()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        await Editor.SetXmpMetadataAsync(doc, SampleXmp, ct: TestContext.Current.CancellationToken);
        var result = doc.GetXmpMetadata();
        result.ShouldNotBeNull();
        result.ShouldContain("Test Document");
    }

    [Fact]
    public async Task SetXmpMetadata_PageCountUnchanged()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        await Editor.SetXmpMetadataAsync(doc, SampleXmp, ct: TestContext.Current.CancellationToken);
        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task SetThenRemoveMetadata_ReturnsNull()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        await Editor.SetXmpMetadataAsync(doc, SampleXmp, ct: TestContext.Current.CancellationToken);
        await Editor.RemoveMetadataAsync(doc, ct: TestContext.Current.CancellationToken);
        doc.GetXmpMetadata().ShouldBeNull();
    }

    [Fact]
    public async Task SetXmpMetadata_PersistsAfterSave()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        await Editor.SetXmpMetadataAsync(doc, SampleXmp, ct: TestContext.Current.CancellationToken);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms, ct: TestContext.Current.CancellationToken);
        ms.Position = 0;
        await using var reloaded = await LoadAsync(ms, ct: TestContext.Current.CancellationToken);
        var result = reloaded.GetXmpMetadata();
        result.ShouldNotBeNull();
        result.ShouldContain("Test Document");
    }

    [Fact]
    public async Task RemoveMetadata_WhenNoMetadata_NoError()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage(), ct: TestContext.Current.CancellationToken);
        await Should.NotThrowAsync(() => Editor.RemoveMetadataAsync(doc));
    }
}
