using System.Text.RegularExpressions;
using Unchained.Drawing.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
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
        var existing = adapter.Core.CollectObjects().ToList();
        var maxObj = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;

        // Build the /Info dictionary — merge with existing entries if present.
        var infoEntries = new Dictionary<string, PdfObject>();

        // Preserve existing /Info entries.
        if (adapter.Core.Info is { } existingInfo)
        {
            foreach (var (key, value) in existingInfo.Entries)
                infoEntries[key] = value;
        }

        if (ToStr(metadata.Title) is { } title) infoEntries["Title"] = title;
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
                    : o)
                .ToList();

            var rootRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
            var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Size.Value] = new PdfInteger(maxObj + 1),
                [PdfName.Root.Value] = rootRef,
                [PdfName.Info.Value] = existingRef
            });

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

            var rootRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
            var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Size.Value] = new PdfInteger(infoObjNum + 1),
                [PdfName.Root.Value] = rootRef,
                [PdfName.Info.Value] = infoRef
            });

            var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(objects, trailer);
            adapter.ReplaceCore(newDoc.Core);
        }

        return;

        // Write only the non-null fields from the supplied metadata.
        static PdfString? ToStr(string? value) =>
            value is null ? null : PdfString.FromLatin1(value);
    }

    internal static void RemovePdfaCompliance(PdfDocumentAdapter adapter)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            return;

        if (existing[catalogIdx].Value is not PdfDictionary catalogDict)
            return;

        // Remove /OutputIntents from catalog.
        var entries = new Dictionary<string, PdfObject>(catalogDict.Entries);
        entries.Remove("OutputIntents");

        // Strip pdfaid properties from XMP if present.
        if (entries.TryGetValue("Metadata", out var metaObj))
        {
            var metaStream = metaObj switch
            {
                PdfStream s => s,
                PdfIndirectReference r =>
                    adapter.Core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
                _ => null
            };
            if (metaStream is not null)
            {
                var xmp = StreamFilters.Decode(metaStream).Span.FromUtf8Span();
                var cleaned = StripXmpNamespace(xmp, "pdfaid");
                var cleanedBytes = cleaned.ToUtf8Span();
                var newStreamDict = new PdfDictionary(
                    new Dictionary<string, PdfObject>(metaStream.Dictionary.Entries)
                    {
                        ["Length"] = new PdfInteger(cleanedBytes.Length)
                    });
                var newStream = new PdfStream(newStreamDict, cleanedBytes.ToArray());

                if (metaObj is PdfIndirectReference metaRef)
                {
                    var metaIdx = existing.FindIndex(o => o.ObjectNumber == metaRef.ObjectNumber);

                    if (metaIdx >= 0)
                        existing[metaIdx] = new PdfIndirectObject(metaRef.ObjectNumber, 0, newStream);
                }
                else
                    entries["Metadata"] = newStream;
            }
        }

        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(entries));
        MutationHelper.SerializeAndReplace(adapter, existing);
    }

    internal static void RemovePdfUaCompliance(PdfDocumentAdapter adapter)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0)
            return;

        if (existing[catalogIdx].Value is not PdfDictionary catalogDict)
            return;

        // Remove /MarkInfo from catalog.
        var entries = new Dictionary<string, PdfObject>(catalogDict.Entries);
        entries.Remove(PdfName.MarkInfo.Value);

        // Strip pdfuaid properties from XMP if present.
        if (entries.TryGetValue("Metadata", out var metaObj))
        {
            var metaStream = metaObj switch
            {
                PdfStream s => s,
                PdfIndirectReference r =>
                    adapter.Core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
                _ => null
            };
            if (metaStream is not null)
            {
                var xmp = StreamFilters.Decode(metaStream).Span.FromUtf8Span();
                var cleaned = StripXmpNamespace(xmp, "pdfuaid");
                var cleanedBytes = cleaned.ToUtf8Span();
                var newStreamDict = new PdfDictionary(
                    new Dictionary<string, PdfObject>(metaStream.Dictionary.Entries)
                    {
                        ["Length"] = new PdfInteger(cleanedBytes.Length)
                    });
                var newStream = new PdfStream(newStreamDict, cleanedBytes.ToArray());

                if (metaObj is PdfIndirectReference metaRef)
                {
                    var metaIdx = existing.FindIndex(o => o.ObjectNumber == metaRef.ObjectNumber);
                    if (metaIdx >= 0)
                        existing[metaIdx] = new PdfIndirectObject(metaRef.ObjectNumber, 0, newStream);
                }
                else
                    entries["Metadata"] = newStream;
            }
        }

        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(entries));
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
            string.Empty);

        // Remove <nsPrefix:xxx>...</nsPrefix:xxx> elements.
        cleaned = Regex.Replace(
            cleaned,
            $"<{nsPrefix}:[^>]*/?>.*?</{nsPrefix}:[^>]+>",
            string.Empty,
            RegexOptions.Singleline);

        // Remove self-closing <nsPrefix:xxx ... /> elements.
        cleaned = Regex.Replace(
            cleaned,
            $@"<{nsPrefix}:[^/]*/\s*>",
            string.Empty);

        return cleaned;
    }
}
