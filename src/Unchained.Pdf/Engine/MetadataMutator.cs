using System.Text.RegularExpressions;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine;

/// <summary>
///     In-place document mutations that read or rewrite the <c>/Info</c> dictionary and XMP
///     metadata: setting document metadata, and removing PDF/A or PDF/UA compliance markers.
///     Extracted from <see cref="DocumentProcessor" />; each method follows the shared
///     collect-patch-serialize-swap pattern via <see cref="MutationHelper" /> or
///     <see cref="ObjectGraphBuilder" />.
/// </summary>
internal static class MetadataMutator
{
    internal static void SetMetadata(PdfDocumentAdapter adapter, DocumentMetadata metadata)
    {
        var (existing, maxObj) = MutationHelper.CollectWithMax(adapter);

        // Build the /Info dictionary — merge with existing entries if present.
        var infoEntries = new Dictionary<string, PdfObject>();

        // Preserve existing /Info entries.
        if (adapter.Core.Info is { } existingInfo)
        {
            foreach (var (key, value) in existingInfo.Entries)
                infoEntries[key] = value;
        }

        if (ToStr(metadata.Title) is { } title) infoEntries[PdfName.Title.Value] = title;
        if (ToStr(metadata.Author) is { } author) infoEntries["Author"] = author;
        if (ToStr(metadata.Subject) is { } subject) infoEntries["Subject"] = subject;
        if (ToStr(metadata.Keywords) is { } keywords) infoEntries["Keywords"] = keywords;
        if (ToStr(metadata.Creator) is { } creator) infoEntries["Creator"] = creator;
        if (ToStr(metadata.Producer) is { } producer) infoEntries["Producer"] = producer;

        var infoDict = new PdfDictionary(infoEntries);

        // Check if an /Info object already exists in the trailer.
        if (adapter.Core.Trailer[PdfName.Info] is PdfIndirectReference existingRef)
        {
            // Replace the existing /Info object in-place.
            var objects = existing
                .Select(o => o.ObjectNumber == existingRef.ObjectNumber
                    ? new PdfIndirectObject(o.ObjectNumber, o.Generation, infoDict)
                    : o
                )
                .ToList();

            var rootRef = MutationHelper.GetCatalogRef(adapter);
            var trailer = new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    [PdfName.Size.Value] = new PdfInteger(maxObj + 1),
                    [PdfName.Root.Value] = rootRef,
                    [PdfName.Info.Value] = existingRef
                }
            );

            var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(objects, trailer);
            adapter.ReplaceCore(newDoc.Core);
        }
        else
        {
            // Add a new /Info object.
            var infoObjNum = maxObj + 1;
            var infoRef = new PdfIndirectReference(infoObjNum, 0);
            var objects = existing
                .Append(new PdfIndirectObject(infoObjNum, 0, infoDict))
                .ToList();

            var rootRef = MutationHelper.GetCatalogRef(adapter);
            var trailer = new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    [PdfName.Size.Value] = new PdfInteger(infoObjNum + 1),
                    [PdfName.Root.Value] = rootRef,
                    [PdfName.Info.Value] = infoRef
                }
            );

            var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(objects, trailer);
            adapter.ReplaceCore(newDoc.Core);
        }

        return;

        // Write only the non-null fields from the supplied metadata.
        static PdfString? ToStr(string? value) =>
            value is null ? null : PdfString.FromLatin1(value);
    }

    internal static void RemovePdfaCompliance(PdfDocumentAdapter adapter) =>
        RemoveComplianceMarkers(adapter, "OutputIntents", "pdfaid");

    internal static void RemovePdfUaCompliance(PdfDocumentAdapter adapter) =>
        RemoveComplianceMarkers(adapter, PdfName.MarkInfo.Value, PdfConstants.PdfAIdentifier);

    /// <summary>
    ///     Removes a compliance marker from the catalog and strips the corresponding XMP
    ///     namespace from the document metadata stream, then serializes. Shared by
    ///     <see cref="RemovePdfaCompliance" /> (<c>/OutputIntents</c> + <c>pdfaid</c>) and
    ///     <see cref="RemovePdfUaCompliance" /> (<c>/MarkInfo</c> + <c>pdfuaid</c>).
    /// </summary>
    private static void RemoveComplianceMarkers(
        PdfDocumentAdapter adapter,
        string catalogKeyToRemove,
        string xmpNamespacePrefix
    )
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var (found, catalogIdx, catalogDict) = MutationHelper.TryGetCatalogDict(adapter, existing);
        if (!found) return;

        // Remove the compliance marker from the catalog.
        var entries = new Dictionary<string, PdfObject>(catalogDict.Entries);
        entries.Remove(catalogKeyToRemove);

        // Strip the compliance namespace's properties from XMP if present.
        if (entries.TryGetValue("Metadata", out var metaObj))
        {
            var metaStream = adapter.Core.ResolveStream(metaObj);
            if (metaStream is not null)
            {
                var xmp = StreamFilters.Decode(metaStream).Span.FromUtf8Span();
                var cleaned = StripXmpNamespace(xmp, xmpNamespacePrefix);
                var cleanedBytes = cleaned.ToUtf8Span();
                var newStreamDict = new PdfDictionary(
                    new Dictionary<string, PdfObject>(metaStream.Dictionary.Entries)
                    {
                        [PdfName.Length.Value] = new PdfInteger(cleanedBytes.Length)
                    }
                );
                var newStream = new PdfStream(newStreamDict, cleanedBytes.ToArray());

                if (metaObj is PdfIndirectReference metaRef)
                {
                    var metaIdx = existing.FindIndex(o => o.ObjectNumber == metaRef.ObjectNumber);

                    if (metaIdx >= 0)
                        existing[metaIdx] = new PdfIndirectObject(metaRef.ObjectNumber, 0, newStream);
                }
                else
                    entries[PdfName.Metadata.Value] = newStream;
            }
        }

        existing[catalogIdx] = new PdfIndirectObject(existing[catalogIdx].ObjectNumber, 0, new PdfDictionary(entries));
        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    /// <summary>
    ///     Removes all XML elements and attributes that belong to
    ///     <paramref name="nsPrefix" /> from an XMP string.
    ///     Uses simple string manipulation to avoid a full XML parse/rewrite cycle.
    /// </summary>
    private static string StripXmpNamespace(string xmp, string nsPrefix)
    {
        // Remove xmlns:nsPrefix="..." declarations.
        var cleaned = Regex.Replace(
            xmp,
            $"""
             \s+xmlns:{nsPrefix}="[^"]*"
             """,
            string.Empty
        );

        // Remove <nsPrefix:xxx>...</nsPrefix:xxx> elements.
        cleaned = Regex.Replace(
            cleaned,
            $"<{nsPrefix}:[^>]*/?>.*?</{nsPrefix}:[^>]+>",
            string.Empty,
            RegexOptions.Singleline
        );

        // Remove self-closing <nsPrefix:xxx ... /> elements.
        cleaned = Regex.Replace(
            cleaned,
            $@"<{nsPrefix}:[^/]*/\s*>",
            string.Empty
        );

        return cleaned;
    }
}
