using System.Text;
using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Direct unit tests for the internal <see cref="PageContentReader.GetContentOperators" /> entry
///     point, exercising the content-collection and form-expansion edge branches that the integration
///     fixtures (indirect-reference content) do not reach: direct-stream content, content arrays with
///     non-stream elements, missing content, non-form/non-stream <c>Do</c> targets, and form decode
///     failures.
/// </summary>
public sealed class PageContentReaderTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    private static PdfStream ContentStream(string content) =>
        new(
            new PdfDictionary(new Dictionary<string, PdfObject> { ["Length"] = new PdfInteger(content.Length) }),
            Encoding.Latin1.GetBytes(content)
        );

    private static PdfDictionary Page(params (string Key, PdfObject Value)[] entries)
    {
        var d = new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page };
        foreach (var (k, v) in entries) d[k] = v;
        return new PdfDictionary(d);
    }

    [Fact]
    public void GetContentOperators_NoContents_ReturnsEmpty() =>
        PageContentReader.GetContentOperators(Page(), Core()).ShouldBeEmpty();

    [Fact]
    public void GetContentOperators_DirectStreamContents_Parses()
    {
        var page = Page(("Contents", ContentStream("0 0 10 10 re f")));
        var ops = PageContentReader.GetContentOperators(page, Core());
        ops.ShouldContain(static o => o.Name == "re");
        ops.ShouldContain(static o => o.Name == "f");
    }

    [Fact]
    public void GetContentOperators_EmptyStream_ReturnsEmpty()
    {
        var page = Page(("Contents", ContentStream(string.Empty)));
        PageContentReader.GetContentOperators(page, Core()).ShouldBeEmpty();
    }

    [Fact]
    public void GetContentOperators_ContentsArrayOfDirectStreams_Concatenates()
    {
        var page = Page(("Contents", new PdfArray([ContentStream("0 0 5 5 re f"), ContentStream("5 5 5 5 re f")])));
        // The array path keeps only indirect references; direct streams yield no streams → empty.
        var ops = PageContentReader.GetContentOperators(page, Core());
        ops.ShouldBeEmpty();
    }

    [Fact]
    public void GetContentOperators_ContentsUnsupportedType_ReturnsEmpty()
    {
        var page = Page(("Contents", new PdfInteger(5)));
        PageContentReader.GetContentOperators(page, Core()).ShouldBeEmpty();
    }

    [Fact]
    public void GetContentOperators_DoTargetNotAStream_LeavesDoIntact()
    {
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["XObject"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["Fm0"] = new PdfInteger(1) })
            }
        );
        var page = Page(("Contents", ContentStream("/Fm0 Do")), ("Resources", resources));

        var ops = PageContentReader.GetContentOperators(page, Core());
        // Non-stream target → Do left intact (no expansion).
        ops.ShouldContain(static o => o.Name == "Do");
    }

    [Fact]
    public void GetContentOperators_DoTargetImageXObject_LeavesDoIntact()
    {
        var imageStream = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject> { ["Subtype"] = PdfName.Get("Image") }),
            ReadOnlyMemory<byte>.Empty
        );
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["XObject"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["Im0"] = imageStream })
            }
        );
        var page = Page(("Contents", ContentStream("/Im0 Do")), ("Resources", resources));

        var ops = PageContentReader.GetContentOperators(page, Core());
        ops.ShouldContain(static o => o.Name == "Do");
    }

    [Fact]
    public void GetContentOperators_FormXObject_ExpandsWithMatrix()
    {
        var form = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    ["Subtype"] = PdfName.Get("Form"),
                    ["Matrix"] = new PdfArray(
                        [
                            new PdfInteger(1), new PdfInteger(0), new PdfInteger(0),
                            new PdfInteger(1), new PdfInteger(5), new PdfInteger(5)
                        ]
                    )
                }
            ),
            Encoding.Latin1.GetBytes("0 0 10 10 re f")
        );
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["XObject"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["Fm0"] = form })
            }
        );
        var page = Page(("Contents", ContentStream("/Fm0 Do")), ("Resources", resources));

        var ops = PageContentReader.GetContentOperators(page, Core());
        ops.ShouldContain(static o => o.Name == "q");
        ops.ShouldContain(static o => o.Name == "cm");
        ops.ShouldContain(static o => o.Name == "Q");
        ops.ShouldContain(static o => o.Name == "re");
        ops.ShouldNotContain(static o => o.Name == "Do");
    }

    [Fact]
    public void GetContentOperators_FormDecodeThrows_EmitsBalancedQWithoutContent()
    {
        // Form XObject whose /Filter is invalid → StreamFilters.Decode throws; the catch emits a
        // balancing Q and skips the form content (lines 104-108).
        var form = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    ["Subtype"] = PdfName.Get("Form"),
                    ["Filter"] = PdfName.Get("NonexistentFilter")
                }
            ),
            Encoding.Latin1.GetBytes("garbage")
        );
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["XObject"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["Fm0"] = form })
            }
        );
        var page = Page(("Contents", ContentStream("/Fm0 Do")), ("Resources", resources));

        var ops = PageContentReader.GetContentOperators(page, Core());
        // q opened, decode failed → Q emitted, no form body operators, Do consumed.
        ops.ShouldContain(static o => o.Name == "q");
        ops.ShouldContain(static o => o.Name == "Q");
        ops.ShouldNotContain(static o => o.Name == "Do");
    }

    [Fact]
    public void GetContentOperators_ContentsReferenceToNonStream_ReturnsEmpty()
    {
        // /Contents is an array element referencing an object that resolves to a non-stream →
        // TryResolveStream yields nothing (line 152).
        var page = Page(("Contents", new PdfArray([new PdfIndirectReference(1, 0)])));
        // Object 1 in the single-page fixture is the catalog dictionary, not a stream.
        PageContentReader.GetContentOperators(page, Core()).ShouldBeEmpty();
    }
}
