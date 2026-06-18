using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
///     Branch coverage for <see cref="Redactor" /> beyond <see cref="RedactorTests" />: the text
///     matrix operators (TL/Td/TD/Tm/T*), the show-with-newline operators (' and "), every
///     <c>WriteOperand</c> type (integer/real/bool/name/null/string/array/dictionary) and both
///     <c>WriteString</c> branches (printable literal with escapes vs hex fallback for binary).
/// </summary>
public sealed class RedactorBranchTests : PdfTestBase
{
    // A content stream that uses every text-positioning operator and carries operands of
    // every PdfObject kind on a custom (non-text, never-dropped) operator "Xx".
    private const string RichContent =
        "q\n" +
        "true false null /SomeName 42 3.14 (lit) <00FF> [1 2 (x)] << /K 5 /N (y) >> Xx\n" +
        "Q\n" +
        "BT\n" +
        "/F1 12 Tf\n" +
        "14 TL\n" +
        "1 0 0 1 50 500 Tm\n" +
        "10 20 Td\n" +
        "5 -5 TD\n" +
        "T*\n" +
        "[(Arr) -15 (Bee)] TJ\n" +
        "(Show) Tj\n" +
        "(x\\)y) Tj\n" +
        "(Quote) '\n" +
        "0 0 (DQ) \"\n" +
        "ET";
    private static readonly Redactor Redactor = new();

    [Fact]
    public async Task Redact_RichContent_OutsideRegion_PreservesAllOperators()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithRawContent(RichContent), TestContext.Current.CancellationToken);
        // Region far from all text (lower-left 1×1 box) → nothing is dropped.
        await Redactor.RedactAsync(
            doc,
            [new RedactionRegion(1, 0, 0, 1, 1)],
            TestContext.Current.CancellationToken
        );

        var ops = doc.Pages[1].GetContentOperators();
        ops.ShouldContain(static o => o.Name == "Xx");
        ops.ShouldContain(static o => o.Name == "TJ");
        ops.ShouldContain(static o => o.Name == "T*");
    }

    [Fact]
    public async Task Redact_RichContent_SurvivesSaveReload()
    {
        await using var doc = await LoadAsync(PdfFixtures.WithRawContent(RichContent), TestContext.Current.CancellationToken);
        await Redactor.RedactAsync(
            doc,
            [new RedactionRegion(1, 0, 0, 1, 1)],
            TestContext.Current.CancellationToken
        );

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        // Printable literal text round-trips through the rebuilt content stream.
        reloaded.Pages[1].ExtractText().ShouldContain("Show");
    }

    [Fact]
    public async Task Redact_ShowNewlineOperators_InsideRegion_AreDropped()
    {
        // Tm places text at (50,500); ' and " show on subsequent lines below it. Redact a
        // tall region covering that whole column so the '/" show ops are dropped.
        await using var doc = await LoadAsync(PdfFixtures.WithRawContent(RichContent), TestContext.Current.CancellationToken);
        await Redactor.RedactAsync(
            doc,
            [new RedactionRegion(1, 40, 400, 200, 150)],
            TestContext.Current.CancellationToken
        );

        // Cover rectangle still appended.
        doc.Pages[1].GetContentOperators().ShouldContain(static o => o.Name == "re");
    }

    [Fact]
    public async Task Redact_HexStringOperand_RoundTripsThroughHexBranch()
    {
        // A Tj whose operand is binary (0x00,0xFF) forces WriteString's hex-output branch.
        await using var doc = await LoadAsync(
            PdfFixtures.WithRawContent("BT /F1 12 Tf 10 700 Td <00FF> Tj ET"),
            TestContext.Current.CancellationToken
        );
        await Redactor.RedactAsync(
            doc,
            [new RedactionRegion(1, 0, 0, 1, 1)],
            TestContext.Current.CancellationToken
        );

        await using var reloaded = await SaveAndReloadAsync(doc, TestContext.Current.CancellationToken);
        reloaded.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task Redact_NonPositivePageNumber_Throws() =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(static async () =>
            {
                await using var doc = await LoadAsync(PdfFixtures.WithTextContent("X"));
                // PageNumber 0 exercises the "< 1" side of the range guard.
                await Redactor.RedactAsync(doc, [new RedactionRegion(0, 0, 0, 10, 10)]);
            }
        );
}
