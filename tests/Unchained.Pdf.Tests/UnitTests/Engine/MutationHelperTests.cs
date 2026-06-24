using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Direct unit coverage for <see cref="MutationHelper" />'s shared casting guard: the success
///     path (a real <see cref="PdfDocumentAdapter" />) and the wrong-document-type throw.
/// </summary>
public sealed class MutationHelperTests
{
    [Fact]
    public void Cast_RealAdapter_ReturnsAdapter()
    {
        using var doc = Unchained.Pdf.Document.PdfDocumentCore.Parse(PdfFixtures.SinglePage());
        var adapter = new PdfDocumentAdapter(doc);
        MutationHelper.Cast("document", adapter).ShouldBeSameAs(adapter);
    }

    [Fact]
    public void Cast_ForeignDocument_ThrowsArgumentException()
    {
        var ex = Should.Throw<ArgumentException>(static () => MutationHelper.Cast("document", new object()));
        ex.ParamName.ShouldBe("document");
    }
}
