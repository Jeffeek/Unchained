using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine.PageResources;

/// <summary>
///     Shared page-dictionary rebuilding for operations that add content to an existing page:
///     appends (or prepends) a content stream to <c>/Contents</c> and merges named resources
///     (fonts, image XObjects, …) into <c>/Resources</c> without disturbing existing entries.
///     Used by <see cref="StampApplier" /> and <see cref="PageContentEditor" />.
/// </summary>
internal static class PageContentComposer
{
    /// <summary>
    ///     Returns a copy of <paramref name="page" /> with <paramref name="contentRef" /> added to
    ///     its <c>/Contents</c> (prepended when <paramref name="prepend" /> is <see langword="true" />,
    ///     so the new content paints behind existing content) and each entry in
    ///     <paramref name="resources" /> merged into <c>/Resources/&lt;Category&gt;</c>.
    /// </summary>
    internal static PdfDictionary RebuildPage(
        PdfDictionary page,
        PdfIndirectReference contentRef,
        bool prepend,
        IEnumerable<(string Category, string Key, PdfObject Ref)> resources,
        PdfDocumentCore core
    )
    {
        var entries = new Dictionary<string, PdfObject>(page.Entries)
        {
            [PdfName.Contents.Value] = MergeContents(page[PdfName.Contents], contentRef, prepend),
            [PdfName.Resources.Value] = MergeResources(core.ResolveDict(page[PdfName.Resources]), resources, core)
        };
        return new PdfDictionary(entries);
    }

    private static PdfObject MergeContents(PdfObject? existing, PdfObject contentRef, bool prepend)
    {
        if (existing is null)
            return contentRef;

        var list = existing is PdfArray a ? a.Elements.ToList() : [existing];
        var all = prepend
            ? new[] { contentRef }.Concat(list).ToArray()
            : [.. list, contentRef];
        return new PdfArray(all);
    }

    private static PdfDictionary MergeResources(
        PdfDictionary? existingResources,
        IEnumerable<(string Category, string Key, PdfObject Ref)> resources,
        PdfDocumentCore core
    )
    {
        var resourceEntries = existingResources?.Entries.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
                              ?? [];

        foreach (var group in resources.GroupBy(static r => r.Category))
        {
            var categoryDict = core.ResolveDict(resourceEntries.GetValueOrDefault(group.Key));
            var categoryEntries = categoryDict?.Entries.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
                                  ?? [];
            foreach (var (_, key, refObj) in group)
                categoryEntries[key] = refObj;
            resourceEntries[group.Key] = new PdfDictionary(categoryEntries);
        }

        return new PdfDictionary(resourceEntries);
    }
}
