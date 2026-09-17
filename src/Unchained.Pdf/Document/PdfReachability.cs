using Unchained.Pdf.Core;

namespace Unchained.Pdf.Document;

/// <summary>
///     Object-graph reachability walk shared by page-level operations (page organisation,
///     page-range merging). Starting from a set of page leaves, returns every object number
///     transitively referenced by indirect reference. References are followed by object number
///     (no inlining), so surviving objects stay byte-for-byte identical to the source.
/// </summary>
internal static class PdfReachability
{
    /// <summary>
    ///     Walks the object graph from <paramref name="leaves" /> and returns every reachable
    ///     object number, including the leaves themselves. Callers that must prevent the walk
    ///     from climbing the page tree (via <c>/Parent</c>) should strip <c>/Parent</c> from the
    ///     leaf dictionaries before passing them in. Objects free in the old xref are skipped
    ///     defensively.
    /// </summary>
    internal static HashSet<int> CollectReachableFromLeaves(
        IEnumerable<PdfIndirectObject> leaves,
        PdfDocumentCore core
    )
    {
        var reachable = new HashSet<int>();
        var queue = new Queue<PdfObject>();

        foreach (var leaf in leaves)
        {
            _ = reachable.Add(leaf.ObjectNumber);
            queue.Enqueue(leaf.Value);
        }

        while (queue.Count > 0)
        {
            var obj = queue.Dequeue();
            switch (obj)
            {
                case PdfIndirectReference r:
                    if (reachable.Add(r.ObjectNumber))
                    {
                        try
                        {
                            queue.Enqueue(core.ResolveIndirect(r.ObjectNumber).Value);
                        }
                        catch (PdfException)
                        {
                            // Free in the old xref (belonged to a deleted page) — nothing to walk.
                        }
                    }

                break;
                case PdfArray arr:
                    foreach (var el in arr.Elements)
                        queue.Enqueue(el);
                break;
                case PdfDictionary dict:
                    foreach (var entry in dict.Entries.Values)
                        queue.Enqueue(entry);
                break;
                case PdfStream stream:
                    foreach (var entry in stream.Dictionary.Entries.Values)
                        queue.Enqueue(entry);
                break;
            }
        }

        return reachable;
    }
}
