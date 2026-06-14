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
    )
    {
        // Leaf: /Nums array of (integer-key, label-dict) pairs.
        if (node.Get<PdfArray>(PdfName.Nums) is { } nums)
        {
            for (var i = 0; i + 1 < nums.Count; i += 2)
            {
                var pageIdx = (int)(nums[i] is PdfInteger pi ? pi.Value : 0);
                var labelDict = core.ResolveDict(nums[i + 1]);

                if (labelDict is null) continue;

                var style = ParseStyle(labelDict.GetName("S"));
                var prefix = labelDict[PdfName.P] is PdfString ps
                    ? Encoding.Latin1.GetString(ps.Bytes.Span)
                    : null;
                var first = (int)(labelDict.Get<PdfInteger>("St")?.Value ?? 1);
                result.Add(new PageLabelRange(pageIdx, style, prefix, first));
            }
        }

        // Intermediate: /Kids array.
        if (node.Get<PdfArray>(PdfName.Kids) is not { } kids)
            return;

        foreach (var kid in kids.Elements)
        {
            var childDict = kid is PdfIndirectReference kr
                ? core.ResolveIndirect(kr.ObjectNumber).Value as PdfDictionary
                : kid as PdfDictionary;
            if (childDict is not null) CollectNumberTree(childDict, core, result);
        }
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    private static void WritePageLabels(
        PdfDocumentAdapter adapter,
        IEnumerable<PageLabelRange> ranges
    )
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0) throw new PdfException("Catalog not found.");

        var catalogDict = existing[catalogIdx].Value as PdfDictionary ?? throw new PdfException("Catalog is not a dictionary.");

        // Build flat /Nums array: [ pageIdx0 labelDict0  pageIdx1 labelDict1 ... ]
        var numsArray = new List<PdfObject>();
        foreach (var range in ranges.OrderBy(static r => r.StartPageIndex))
        {
            numsArray.Add(new PdfInteger(range.StartPageIndex));
            numsArray.Add(BuildLabelDict(range));
        }

        var pageLabelsDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Nums.Value] = new PdfArray(numsArray)
        });

        var catalogEntries = new Dictionary<string, PdfObject>(catalogDict.Entries)
        {
            ["PageLabels"] = pageLabelsDict
        };
        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(catalogEntries));

        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    private static void RemovePageLabels(PdfDocumentAdapter adapter)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0) return;

        if (existing[catalogIdx].Value is not PdfDictionary catalogDict) return;

        var entries = new Dictionary<string, PdfObject>(catalogDict.Entries);
        if (!entries.Remove("PageLabels")) return;

        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(entries));
        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PdfDictionary BuildLabelDict(PageLabelRange range)
    {
        var entries = new Dictionary<string, PdfObject>();
        var styleStr = StyleToString(range.Style);
        if (styleStr is not null) entries["S"] = PdfName.Get(styleStr);
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
