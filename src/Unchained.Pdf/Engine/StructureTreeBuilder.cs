using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Represents a single tagged content item collected during content-stream construction.
///     Each item corresponds to one <c>BDC</c>/<c>EMC</c> pair in a page's content stream.
/// </summary>
internal sealed class TaggedContentItem(
    string structureType,
    int mcid,
    int pageIndex
)
{
    /// <summary>
    ///     Standard PDF structure type name — e.g. <c>"P"</c>, <c>"H1"</c>, <c>"Figure"</c>,
    ///     <c>"Table"</c>, <c>"TR"</c>, <c>"TH"</c>, <c>"TD"</c>, <c>"L"</c>,
    ///     <c>"LI"</c>, <c>"LBody"</c>, <c>"Code"</c>.
    /// </summary>
    internal string StructureType { get; } = structureType;

    /// <summary>Marked-content identifier — unique within the page.</summary>
    internal int Mcid { get; } = mcid;

    /// <summary>Zero-based page index this item belongs to.</summary>
    internal int PageIndex { get; } = pageIndex;

    /// <summary>Optional alternative text for Figure elements (written to <c>/Alt</c>).</summary>
    internal string? AltText { get; init; }
}

/// <summary>
///     Builds the PDF logical structure tree (<c>/StructTreeRoot</c>) and
///     <c>/ParentTree</c> number tree from a flat list of <see cref="TaggedContentItem" />
///     instances collected during content-stream construction.
///     <para>
///         The resulting objects are added to an <see cref="ObjectGraphBuilder" /> and the
///         <c>/StructTreeRoot</c> indirect reference is returned for injection into the catalog.
///     </para>
/// </summary>
internal static class StructureTreeBuilder
{
    /// <summary>
    ///     Builds the full structure tree and appends all required objects to
    ///     <paramref name="builder" />. Returns a reference to the <c>/StructTreeRoot</c>
    ///     object that must be added to the document catalog.
    /// </summary>
    /// <param name="items">
    ///     All tagged content items collected from all pages, in MCID order per page.
    /// </param>
    /// <param name="pageRefs">
    ///     Indirect references to each page dict, indexed by zero-based page number.
    ///     Required to populate <c>/Pg</c> entries in marked-content references.
    /// </param>
    /// <param name="builder">
    ///     The object graph builder that owns object-number allocation. All structure
    ///     tree objects are added here.
    /// </param>
    internal static PdfIndirectReference Build(
        IReadOnlyList<TaggedContentItem> items,
        IReadOnlyList<PdfIndirectReference> pageRefs,
        ObjectGraphBuilder builder
    )
    {
        // ── 1. Group items by page ─────────────────────────────────────────────
        var byPage = items
            .GroupBy(static i => i.PageIndex)
            .ToDictionary(static g => g.Key, static g => g.OrderBy(static i => i.Mcid).ToList());

        // ── 2. Group consecutive items with the same structure type into elements ──
        // Each struct element holds one or more MCIDs from the same page.
        // This produces a flat Document → [elements] tree.
        var elemRefs = new List<PdfIndirectReference>();
        // parentTree maps: MCID-key (globally unique across all pages) → structElem ref.
        // We use a flat number tree with a /Nums array at the root.
        var parentTreeNums = new List<PdfObject>();

        // Reserve the StructTreeRoot object number up front (we need it as /P in elements).
        var rootObjNum = builder.NextNumber();
        var rootRef = new PdfIndirectReference(rootObjNum, 0);

        // Reserve the Document element object number.
        var docElemObjNum = builder.NextNumber();
        var docElemRef = new PdfIndirectReference(docElemObjNum, 0);

        // Process each page's items and build struct elements.
        foreach (var (pageIdx, pageItems) in byPage.OrderBy(static kv => kv.Key))
        {
            var pageRef = pageIdx < pageRefs.Count
                ? pageRefs[pageIdx]
                : pageRefs.LastOrDefault() ?? new PdfIndirectReference(1, 0);

            // Group consecutive same-type items on the same page into one struct element.
            var groups = GroupByType(pageItems);

            foreach (var group in groups)
            {
                var elemObjNum = builder.NextNumber();
                var elemRef = new PdfIndirectReference(elemObjNum, 0);

                // Build the /K array: one MCR dict per MCID in this group.
                var kItems = new List<PdfObject>();
                foreach (var item in group)
                {
                    // Marked-content reference (§14.7.4.2).
                    var mcrEntries = new Dictionary<string, PdfObject>
                    {
                        [PdfName.Type.Value] = PdfName.MCR,
                        [PdfName.Pg.Value] = pageRef,
                        [PdfName.MCID.Value] = new PdfInteger(item.Mcid)
                    };
                    kItems.Add(new PdfDictionary(mcrEntries));

                    // Register in parent tree: MCID → this struct element ref.
                    parentTreeNums.Add(new PdfInteger(item.Mcid));
                    parentTreeNums.Add(elemRef);
                }

                var elemEntries = new Dictionary<string, PdfObject>
                {
                    [PdfName.Type.Value] = PdfName.StructElem,
                    [PdfName.S.Value] = PdfName.Get(group[0].StructureType),
                    [PdfName.P.Value] = docElemRef,
                    [PdfName.Pg.Value] = pageRef,
                    [PdfName.K.Value] = kItems.Count == 1
                        ? kItems[0]
                        : new PdfArray(kItems)
                };

                // Add /Alt for Figure elements.
                var altText = group.FirstOrDefault(static i => i.AltText is not null)?.AltText;
                if (altText is not null)
                    elemEntries[PdfName.Alt.Value] = PdfString.FromUtf16(altText);

                builder.AddAt(elemObjNum, new PdfDictionary(elemEntries));
                elemRefs.Add(elemRef);
            }
        }

        // ── 3. Document struct element ─────────────────────────────────────────
        var docEntries = new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.StructElem,
            [PdfName.S.Value] = PdfName.Document,
            [PdfName.P.Value] = rootRef,
            [PdfName.K.Value] = new PdfArray(elemRefs.Cast<PdfObject>().ToList())
        };
        builder.AddAt(docElemObjNum, new PdfDictionary(docEntries));

        // ── 4. ParentTree number tree ──────────────────────────────────────────
        // Flat leaf node: /Nums [ key1 val1 key2 val2 ... ]
        var parentTreeObjNum = builder.NextNumber();
        var parentTreeRef = new PdfIndirectReference(parentTreeObjNum, 0);
        var parentTreeDict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Nums.Value] = new PdfArray(parentTreeNums)
            }
        );
        builder.AddAt(parentTreeObjNum, parentTreeDict);

        // ── 5. StructTreeRoot ──────────────────────────────────────────────────
        var rootEntries = new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.StructTreeRoot,
            [PdfName.K.Value] = docElemRef,
            [PdfName.ParentTree.Value] = parentTreeRef,
            [PdfName.ParentTreeNextKey.Value] = new PdfInteger(parentTreeNums.Count / 2),
            [PdfName.RoleMap.Value] = new PdfDictionary()
        };
        builder.AddAt(rootObjNum, new PdfDictionary(rootEntries));

        return rootRef;
    }

    /// <summary>
    ///     Groups a page's tagged items into runs of consecutive same-type items.
    ///     Items of different types break the run, starting a new struct element.
    ///     This keeps the structure tree shallow while preserving semantic boundaries.
    /// </summary>
    private static List<List<TaggedContentItem>> GroupByType(IReadOnlyList<TaggedContentItem> items)
    {
        var groups = new List<List<TaggedContentItem>>();
        if (items.Count == 0) return groups;

        var current = new List<TaggedContentItem> { items[0] };
        for (var i = 1; i < items.Count; i++)
        {
            if (items[i].StructureType == current[0].StructureType)
                current.Add(items[i]);
            else
            {
                groups.Add(current);
                current = [items[i]];
            }
        }

        groups.Add(current);
        return groups;
    }
}
