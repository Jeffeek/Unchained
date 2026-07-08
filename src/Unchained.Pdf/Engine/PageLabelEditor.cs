using System.Text;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>Default <see cref="IPageLabelEditor" /> implementation.</summary>
// ReSharper disable once MemberCanBeInternal
public sealed class PageLabelEditor : IPageLabelEditor
{
    /// <inheritdoc />
    public IReadOnlyList<PageLabelRange> GetPageLabels(IPdfDocument document)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        return ReadPageLabels(adapter.Core);
    }

    /// <inheritdoc />
    public Task SetPageLabelsAsync(
        IPdfDocument document,
        IReadOnlyList<PageLabelRange> ranges,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(ranges);
        if (ranges.Count == 0)
            throw new ArgumentException("Ranges must not be empty.", nameof(ranges));
        if (ranges[0].StartPageIndex != 0)
            throw new ArgumentException("First range must start at page index 0.", nameof(ranges));

        var adapter = MutationHelper.Cast(nameof(document), document);
        return Task.Run(() => WritePageLabels(adapter, ranges), ct);
    }

    /// <inheritdoc />
    public Task RemovePageLabelsAsync(
        IPdfDocument document,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = MutationHelper.Cast(nameof(document), document);
        return Task.Run(() => RemovePageLabels(adapter), ct);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    private static IReadOnlyList<PageLabelRange> ReadPageLabels(PdfDocumentCore core)
    {
        var result = new List<PageLabelRange>();
        var pageLabelsObj = core.Catalog[PdfName.PageLabels];
        var root = core.ResolveDict(pageLabelsObj);
        if (root is null) return result;

        CollectNumberTree(root, core, result);
        return result.OrderBy(static r => r.StartPageIndex).ToList();
    }

    private static void CollectNumberTree(
        PdfDictionary node,
        PdfDocumentCore core,
        ICollection<PageLabelRange> result
    ) =>
        MutationHelper.CollectTree(
            node,
            core,
            result,
            PdfName.Nums.Value,
            (_, arr, i, _) =>
            {
                var pageIdx = (int)(arr[i] is PdfInteger pi ? pi.Value : 0);
                var labelDict = core.ResolveDict(arr[i + 1]);
                if (labelDict is null) return null;

                var style = ParseStyle(labelDict.GetName("S"));
                var prefix = labelDict[PdfName.P] is PdfString ps
                    ? Encoding.Latin1.GetString(ps.Bytes.Span)
                    : null;
                var first = (int)(labelDict.Get<PdfInteger>("St")?.Value ?? 1);
                return new PageLabelRange(pageIdx, style, prefix, first);
            }
        );

    // ── Write ─────────────────────────────────────────────────────────────────

    private static void WritePageLabels(
        PdfDocumentAdapter adapter,
        IEnumerable<PageLabelRange> ranges
    )
    {
        var numsArray = new List<PdfObject>();
        foreach (var range in ranges.OrderBy(static r => r.StartPageIndex))
        {
            numsArray.Add(new PdfInteger(range.StartPageIndex));
            numsArray.Add(BuildLabelDict(range));
        }

        var pageLabelsDict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Nums.Value] = new PdfArray(numsArray)
            }
        );

        MutationHelper.ApplyCatalogMutation(
            adapter,
            entries =>
            {
                entries[PdfName.PageLabels.Value] = pageLabelsDict;
            }
        );
    }

    private static void RemovePageLabels(PdfDocumentAdapter adapter) =>
        MutationHelper.TryApplyCatalogMutation(
            adapter,
            static entries =>
            {
                entries.Remove("PageLabels");
            }
        );

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PdfDictionary BuildLabelDict(PageLabelRange range)
    {
        var entries = new Dictionary<string, PdfObject>();
        var styleStr = StyleToString(range.Style);
        if (styleStr is not null) entries[PdfName.S.Value] = PdfName.Get(styleStr);
        if (!string.IsNullOrEmpty(range.Prefix)) entries["P"] = PdfString.FromLatin1(range.Prefix);
        if (range.FirstLabelNumber != 1) entries["St"] = new PdfInteger(range.FirstLabelNumber);

        return new PdfDictionary(entries);
    }

    private static string? StyleToString(PageLabelStyle style) => style switch
    {
        PageLabelStyle.Decimal => "D",
        PageLabelStyle.RomanUpper => "R",
        PageLabelStyle.RomanLower => "r",
        PageLabelStyle.AlphaUpper => "A",
        PageLabelStyle.AlphaLower => "a",
        PageLabelStyle.None => null,
        _ => "D"
    };

    private static PageLabelStyle ParseStyle(string? s) => s switch
    {
        "D" => PageLabelStyle.Decimal,
        "R" => PageLabelStyle.RomanUpper,
        "r" => PageLabelStyle.RomanLower,
        "A" => PageLabelStyle.AlphaUpper,
        "a" => PageLabelStyle.AlphaLower,
        _ => PageLabelStyle.None
    };
}
