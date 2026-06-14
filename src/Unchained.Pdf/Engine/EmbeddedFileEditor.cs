using System.IO.Compression;
using System.Text;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine;

/// <summary>Default <see cref="IEmbeddedFileEditor" /> implementation.</summary>
// ReSharper disable once MemberCanBeInternal
public sealed class EmbeddedFileEditor : IEmbeddedFileEditor
{
    /// <inheritdoc />
    public IReadOnlyList<EmbeddedFile> GetEmbeddedFiles(IPdfDocument document)
    {
        var adapter = MutationHelper.Cast(nameof(document), document);
        return CollectEmbeddedFiles(adapter.Core);
    }

    /// <inheritdoc />
    public Task AddEmbeddedFileAsync(
        IPdfDocument document,
        EmbeddedFile file,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(file);
        var adapter = MutationHelper.Cast(nameof(document), document);
        return Task.Run(() => AddEmbeddedFile(adapter, file), ct);
    }

    /// <inheritdoc />
    public Task RemoveEmbeddedFileAsync(
        IPdfDocument document,
        string name,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var adapter = MutationHelper.Cast(nameof(document), document);
        return Task.Run(() => RemoveEmbeddedFile(adapter, name), ct);
    }

    /// <inheritdoc />
    public Task EnablePortfolioModeAsync(
        IPdfDocument document,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = MutationHelper.Cast(nameof(document), document);
        return Task.Run(() => SetPortfolioMode(adapter, true), ct);
    }

    /// <inheritdoc />
    public Task DisablePortfolioModeAsync(
        IPdfDocument document,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        var adapter = MutationHelper.Cast(nameof(document), document);
        return Task.Run(() => SetPortfolioMode(adapter, false), ct);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    private static IReadOnlyList<EmbeddedFile> CollectEmbeddedFiles(PdfDocumentCore core)
    {
        var result = new List<EmbeddedFile>();
        var names = core.ResolveDict(core.Catalog[PdfName.Names]);
        var efTree = names is not null ? core.ResolveDict(names[PdfName.EmbeddedFiles]) : null;
        if (efTree is null) return result;

        CollectNameTree(efTree, core, result);
        return result;
    }

    private static void CollectNameTree(
        PdfDictionary node,
        PdfDocumentCore core,
        ICollection<EmbeddedFile> result
    )
    {
        // Leaf node: /Names array of (string-key, filespec-dict) pairs.
        if (node.Get<PdfArray>(PdfName.Names) is { } namesArr)
        {
            for (var i = 0; i + 1 < namesArr.Count; i += 2)
            {
                var key = namesArr[i] is PdfString ks
                    ? Encoding.Latin1.GetString(ks.Bytes.Span)
                    : (namesArr[i] as PdfName)?.Value ?? string.Empty;

                var fileSpec = namesArr[i + 1] is PdfIndirectReference r
                    ? core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary
                    : namesArr[i + 1] as PdfDictionary;

                if (fileSpec is null || key.Length == 0) continue;

                var ef = BuildEmbeddedFile(key, fileSpec, core);
                if (ef is not null) result.Add(ef);
            }
        }

        // Intermediate node: /Kids array.
        if (node.Get<PdfArray>(PdfName.Kids) is not { } kids)
            return;

        foreach (var kid in kids.Elements)
        {
            var childDict = kid is PdfIndirectReference kr
                ? core.ResolveIndirect(kr.ObjectNumber).Value as PdfDictionary
                : kid as PdfDictionary;
            if (childDict is not null) CollectNameTree(childDict, core, result);
        }
    }

    private static EmbeddedFile? BuildEmbeddedFile(
        string name,
        PdfDictionary fileSpec,
        PdfDocumentCore core
    )
    {
        var fileName = fileSpec[PdfName.UF] is PdfString uf
            ? Encoding.BigEndianUnicode.GetString(uf.Bytes.Span).TrimStart('﻿')
            : fileSpec[PdfName.F] is PdfString f
                ? Encoding.Latin1.GetString(f.Bytes.Span)
                : name;

        var desc = fileSpec[PdfName.Desc] is PdfString ds
            ? Encoding.Latin1.GetString(ds.Bytes.Span)
            : null;

        var efDict = core.ResolveDict(fileSpec[PdfName.EF]);
        var streamObj = efDict is not null
            ? efDict[PdfName.F] is PdfIndirectReference sr
                ? core.ResolveIndirect(sr.ObjectNumber).Value as PdfStream
                : efDict[PdfName.F] as PdfStream
            : null;

        if (streamObj is null) return null;

        var mimeType = fileSpec[PdfName.Subtype] is PdfName mtn ? mtn.Value : null;

        byte[] data;
        try { data = StreamFilters.Decode(streamObj).ToArray(); }
        catch { data = streamObj.Data.ToArray(); }

        return new EmbeddedFile(name, fileName, desc, mimeType, data);
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    private static void AddEmbeddedFile(PdfDocumentAdapter adapter, EmbeddedFile file)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var maxObj = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;
        var builder = new ObjectGraphBuilder(maxObj + 1);

        // Compress file data with FlateDecode.
        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, true))
                zlib.Write(file.Data);
            compressed = ms.ToArray();
        }

        // Build the embedded file stream.
        var efStreamEntries = new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.EmbeddedFile,
            [PdfName.Filter.Value] = PdfName.FlateDecode,
            [PdfName.Length.Value] = new PdfInteger(compressed.Length),
            ["Params"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Size"] = new PdfInteger(file.Data.Length)
            })
        };
        if (file.MimeType is not null)
            efStreamEntries["Subtype"] = PdfName.Get(file.MimeType.Replace("/", "#2F"));

        var efStreamRef = builder.Add(
            new PdfStream(new PdfDictionary(efStreamEntries), compressed)).ToReference();

        // Build the file specification dictionary.
        var fileSpecEntries = new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Filespec,
            ["F"] = PdfString.FromLatin1(file.FileName),
            ["UF"] = new PdfString(
                new[] { PdfConstants.Utf16BeBomByte0, PdfConstants.Utf16BeBomByte1 }
                    .Concat(Encoding.BigEndianUnicode.GetBytes(file.FileName))
                    .ToArray()),
            ["EF"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["F"] = efStreamRef
            })
        };
        if (file.Description is not null)
            fileSpecEntries["Desc"] = PdfString.FromLatin1(file.Description);

        var fileSpecRef = builder.Add(new PdfDictionary(fileSpecEntries)).ToReference();

        // Update /Names /EmbeddedFiles name tree.
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0) throw new PdfException("Catalog not found.");

        var catalogDict = existing[catalogIdx].Value as PdfDictionary ?? throw new PdfException("Catalog is not a dictionary.");
        var catalogEntries = new Dictionary<string, PdfObject>(catalogDict.Entries);

        // Get or create /Names dict.
        var namesRef = catalogEntries.GetValueOrDefault(PdfName.Names.Value);
        var namesDict = namesRef switch
        {
            PdfDictionary d => new Dictionary<string, PdfObject>(d.Entries),
            PdfIndirectReference r =>
                adapter.Core.ResolveIndirect(r.ObjectNumber).Value is PdfDictionary nd
                    ? new Dictionary<string, PdfObject>(nd.Entries)
                    : new Dictionary<string, PdfObject>(),
            _ => new Dictionary<string, PdfObject>()
        };

        // Build a flat /EmbeddedFiles name tree leaf — collect existing + new.
        var efTree = namesDict.GetValueOrDefault("EmbeddedFiles");
        var efDict = adapter.Core.ResolveDict(efTree);

        var existingPairs = new List<PdfObject>();
        if (efDict?.Get<PdfArray>(PdfName.Names) is { } oldNames)
        {
            // Copy existing pairs, removing any entry with the same name.
            for (var i = 0; i + 1 < oldNames.Count; i += 2)
            {
                var existingKey = oldNames[i] is PdfString eks
                    ? Encoding.Latin1.GetString(eks.Bytes.Span)
                    : (oldNames[i] as PdfName)?.Value ?? string.Empty;
                if (existingKey == file.Name)
                {
                    i += 0;
                    continue;
                } // skip duplicate

                existingPairs.Add(oldNames[i]);
                existingPairs.Add(oldNames[i + 1]);
            }
        }

        existingPairs.Add(PdfString.FromLatin1(file.Name));
        existingPairs.Add(fileSpecRef);

        var newEfTreeRef = builder.Add(new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Names.Value] = new PdfArray(existingPairs)
        })).ToReference();

        namesDict["EmbeddedFiles"] = newEfTreeRef;
        var newNamesRef = builder.Add(new PdfDictionary(namesDict)).ToReference();
        catalogEntries[PdfName.Names.Value] = newNamesRef;

        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(catalogEntries));

        var allObjects = existing.Concat(builder.Objects).ToList();
        MutationHelper.SerializeAndReplace(adapter, allObjects);
    }

    private static void RemoveEmbeddedFile(PdfDocumentAdapter adapter, string name)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0) return;

        if (existing[catalogIdx].Value is not PdfDictionary catalogDict) return;

        var namesObj = catalogDict[PdfName.Names];
        var namesDict = adapter.Core.ResolveDict(namesObj);
        if (namesDict is null) return;

        var efObj = namesDict[PdfName.EmbeddedFiles];
        var efDict = adapter.Core.ResolveDict(efObj);
        if (efDict?.Get<PdfArray>(PdfName.Names) is not { } oldNames) return;

        var newPairs = new List<PdfObject>();
        for (var i = 0; i + 1 < oldNames.Count; i += 2)
        {
            var key = oldNames[i] is PdfString ks
                ? Encoding.Latin1.GetString(ks.Bytes.Span)
                : (oldNames[i] as PdfName)?.Value ?? string.Empty;
            if (key == name) continue;

            newPairs.Add(oldNames[i]);
            newPairs.Add(oldNames[i + 1]);
        }

        // Rebuild catalog with updated name tree.
        var maxObj = existing.Max(static o => o.ObjectNumber);
        var builder = new ObjectGraphBuilder(maxObj + 1);

        var newEfRef = builder.Add(new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Names.Value] = new PdfArray(newPairs)
        })).ToReference();

        var newNamesEntries = new Dictionary<string, PdfObject>(namesDict.Entries)
        {
            ["EmbeddedFiles"] = newEfRef
        };
        var newNamesRef = builder.Add(new PdfDictionary(newNamesEntries)).ToReference();

        var catalogEntries = new Dictionary<string, PdfObject>(catalogDict.Entries)
        {
            [PdfName.Names.Value] = newNamesRef
        };
        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(catalogEntries));

        var allObjects = existing.Concat(builder.Objects).ToList();
        MutationHelper.SerializeAndReplace(adapter, allObjects);
    }

    private static void SetPortfolioMode(PdfDocumentAdapter adapter, bool enable)
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var catalogRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var catalogIdx = existing.FindIndex(o => o.ObjectNumber == catalogRef.ObjectNumber);
        if (catalogIdx < 0) return;

        if (existing[catalogIdx].Value is not PdfDictionary catalogDict) return;

        var entries = new Dictionary<string, PdfObject>(catalogDict.Entries);

        if (enable)
        {
            if (entries.ContainsKey("Collection")) return; // already enabled

            entries["Collection"] = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Collection,
                ["Sort"] = new PdfDictionary(new Dictionary<string, PdfObject>
                {
                    ["S"] = PdfName.ModDate,
                    ["A"] = PdfBoolean.True
                })
            });
        }
        else
            entries.Remove("Collection");

        existing[catalogIdx] = new PdfIndirectObject(catalogRef.ObjectNumber, 0, new PdfDictionary(entries));
        MutationHelper.SerializeAndReplace(adapter, existing);
    }
}
