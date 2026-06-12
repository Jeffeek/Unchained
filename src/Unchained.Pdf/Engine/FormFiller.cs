using System.Text;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Default <see cref="IFormFiller" /> implementation.
///     Fields are matched by fully-qualified name (dot-separated <c>/T</c> path).
/// </summary>
public sealed class FormFiller : IFormFiller
{
    /// <inheritdoc />
    public Task FillAsync(
        IPdfDocument document,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default
    ) => Task.Run(() => Fill(document, values), ct);

    /// <inheritdoc />
    public Task FlattenAsync(IPdfDocument document, CancellationToken ct = default) =>
        Task.Run(() => Flatten(document), ct);

    // ── Fill ──────────────────────────────────────────────────────────────────

    private static void Fill(IPdfDocument document, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0) return;

        var adapter = document as PdfDocumentAdapter
                      ?? throw new ArgumentException(
                          $"Document was not created by Unchained. Expected {nameof(PdfDocumentAdapter)}, got {document.GetType().Name}.",
                          nameof(document));

        var existing = adapter.Core.CollectObjects();
        var swaps = new Dictionary<int, PdfIndirectObject>();

        // Walk all objects, find field dicts matching requested names.
        // Build a name-to-object map first for all fields.
        var fieldMap = BuildFieldMap(existing, adapter.Core);

        foreach (var (name, newValue) in values)
        {
            if (!fieldMap.TryGetValue(name, out var fieldObj))
                continue;

            var fieldDict = (PdfDictionary)fieldObj.Value;
            var ft = FieldType(fieldDict, adapter.Core);
            var entries = new Dictionary<string, PdfObject>(fieldDict.Entries);

            switch (ft)
            {
                case "Btn":
                    // Checkboxes / radio buttons: the value is an appearance-state NAME
                    // (e.g. the "on" state from /AP /N, or "Off"). Set both /V and /AS so
                    // viewers show the correct state. ISO 32000-1 §12.7.4.2.3.
                {
                    var stateName = ResolveButtonState(fieldDict, newValue, adapter.Core);
                    entries["V"] = PdfName.Get(stateName);
                    entries["AS"] = PdfName.Get(stateName);
                }
                break;

                case "Ch":
                    // Choice fields (combo/list): /V is a text string (or array for multi-
                    // select; single value handled here). §12.7.4.4.
                    entries["V"] = PdfString.FromLatin1(newValue);
                break;

                default:
                    // Tx (text) and anything else: plain text string value.
                    entries["V"] = PdfString.FromLatin1(newValue);
                break;
            }

            swaps[fieldObj.ObjectNumber] = new PdfIndirectObject(fieldObj.ObjectNumber, fieldObj.Generation, new PdfDictionary(entries));
        }

        if (swaps.Count == 0) return;

        var finalObjects = existing
            .Select(o => swaps.GetValueOrDefault(o.ObjectNumber, o))
            .ToList();

        SerializeAndReplace(adapter, finalObjects);
    }

    // ── Flatten ───────────────────────────────────────────────────────────────

    private static void Flatten(IPdfDocument document)
    {
        var adapter = document as PdfDocumentAdapter
                      ?? throw new ArgumentException(
                          $"Document was not created by Unchained. Expected {nameof(PdfDocumentAdapter)}, got {document.GetType().Name}.",
                          nameof(document));

        var existing = adapter.Core.CollectObjects();
        var maxObjNum = existing.Count > 0 ? existing.Max(static o => o.ObjectNumber) : 0;
        var builder = new ObjectGraphBuilder(maxObjNum + 1);

        var fieldMap = BuildFieldMap(existing, adapter.Core);
        var swaps = new Dictionary<int, PdfIndirectObject>();

        // For each Tx field with /AP /N appearance, append to the page it belongs to.
        foreach (var (_, fieldObj) in fieldMap)
        {
            var fieldDict = (PdfDictionary)fieldObj.Value;
            if (fieldDict.GetName("FT") != "Tx")
                continue;

            var ap = ResolveDict(fieldDict[PdfName.Get("AP")], adapter.Core);
            var normalAp = ap is not null ? ResolveStream(ap[PdfName.Get("N")], adapter.Core) : null;
            if (normalAp is null)
                continue;

            // Find which page this widget annotation belongs to.
            if (fieldDict[PdfName.Get("P")] is not PdfIndirectReference pageRef)
                continue;

            var pageObj = existing.FirstOrDefault(o => o.ObjectNumber == pageRef.ObjectNumber);
            if (pageObj is null)
                continue;

            // Add the appearance stream to page /Contents.
            var appearanceBytes = normalAp.Data.ToArray();
            var apStream = builder.Add(new PdfStream(
                new PdfDictionary(new Dictionary<string, PdfObject>
                {
                    [PdfName.Length.Value] = new PdfInteger(appearanceBytes.Length)
                }),
                appearanceBytes));

            var pd = (PdfDictionary)pageObj.Value;
            var existingContents = pd[PdfName.Contents];
            PdfObject newContents;
            if (existingContents is null)
                newContents = apStream.ToReference();
            else
            {
                var existingList = existingContents is PdfArray a
                    ? a.Elements.ToList()
                    : [existingContents];
                newContents = new PdfArray(existingList.Append(apStream.ToReference()).ToArray());
            }

            var pageEntries = new Dictionary<string, PdfObject>(pd.Entries)
            {
                [PdfName.Contents.Value] = newContents
            };
            swaps[pageObj.ObjectNumber] = new PdfIndirectObject(pageObj.ObjectNumber, pageObj.Generation, new PdfDictionary(pageEntries));
        }

        // Remove /AcroForm from catalog.
        var catalogObj = existing.First(static o => o.Value is PdfDictionary d && d.GetName(PdfName.Type.Value) == "Catalog");
        var catDict = (PdfDictionary)catalogObj.Value;
        var catEntries = new Dictionary<string, PdfObject>(catDict.Entries);
        catEntries.Remove(PdfName.AcroForm.Value);
        swaps[catalogObj.ObjectNumber] = new PdfIndirectObject(catalogObj.ObjectNumber, catalogObj.Generation, new PdfDictionary(catEntries));

        var finalObjects = existing
            .Select(o => swaps.GetValueOrDefault(o.ObjectNumber, o))
            .Concat(builder.Objects)
            .ToList();

        SerializeAndReplace(adapter, finalObjects);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Builds a map: fully-qualified field name → PdfIndirectObject for that field.
    private static Dictionary<string, PdfIndirectObject> BuildFieldMap(
        IReadOnlyList<PdfIndirectObject> existing,
        PdfDocumentCore core
    )
    {
        var acroFormObj = existing.FirstOrDefault(static o => o.Value is PdfDictionary d && d.GetName(PdfName.Type.Value) == "Catalog");
        if (acroFormObj is null)
            return new Dictionary<string, PdfIndirectObject>();

        var catalog = (PdfDictionary)acroFormObj.Value;
        var acroForm = ResolveDict(catalog[PdfName.AcroForm], core);
        if (acroForm is null)
            return new Dictionary<string, PdfIndirectObject>();

        var fields = acroForm.Get<PdfArray>(PdfName.Fields);
        if (fields is null)
            return new Dictionary<string, PdfIndirectObject>();

        var result = new Dictionary<string, PdfIndirectObject>();
        CollectFieldMap(fields, string.Empty, existing, result);

        return result;
    }

    private static void CollectFieldMap(
        PdfArray fields,
        string prefix,
        IReadOnlyList<PdfIndirectObject> existing,
        IDictionary<string, PdfIndirectObject> result
    )
    {
        foreach (var elem in fields.Elements)
        {
            if (elem is not PdfIndirectReference r)
                continue;

            var obj = existing.FirstOrDefault(o => o.ObjectNumber == r.ObjectNumber);
            if (obj?.Value is not PdfDictionary dict)
                continue;

            var partialName = dict[PdfName.Get("T")] is PdfString ts
                ? Encoding.Latin1.GetString(ts.Bytes.Span)
                : string.Empty;
            var fullName = prefix.Length > 0 ? $"{prefix}.{partialName}" : partialName;

            var ft = dict.GetName("FT");
            if (ft is null && dict.Get<PdfArray>(PdfName.Kids) is { } kids)
            {
                CollectFieldMap(kids, fullName, existing, result);
                continue;
            }

            result[fullName] = obj;
        }
    }

    private static void SerializeAndReplace(PdfDocumentAdapter adapter, IReadOnlyCollection<PdfIndirectObject> objects)
    {
        var totalMax = objects.Max(static o => o.ObjectNumber);
        var rootRef = adapter.Core.Trailer[PdfName.Root] as PdfIndirectReference ?? throw new PdfException("Trailer missing /Root.");
        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(totalMax + 1),
            [PdfName.Root.Value] = rootRef
        });
        var newDoc = (PdfDocumentAdapter)ObjectGraphBuilder.SerializeToDocument(objects, trailer);
        adapter.ReplaceCore(newDoc.Core);
    }

    private static PdfDictionary? ResolveDict(PdfObject? obj, PdfDocumentCore core) => obj switch
    {
        PdfDictionary d => d,
        PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
        _ => null
    };

    private static PdfStream? ResolveStream(PdfObject? obj, PdfDocumentCore core) => obj switch
    {
        PdfStream s => s,
        PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
        _ => null
    };

    // Resolves a field's type, following /Parent for fields that inherit /FT (§12.7.3.1).
    private static string? FieldType(PdfDictionary field, PdfDocumentCore core)
    {
        var current = field;
        for (var depth = 0; current is not null && depth < 32; depth++)
        {
            if (current.GetName("FT") is { } ft) return ft;
            current = ResolveDict(current["Parent"], core);
        }

        return null;
    }

    // Maps a user-supplied button value to an appearance-state name.
    // Truthy strings (true/on/yes/1/checked/x) select the field's "on" state — the first
    // /AP /N key that is not "Off"; falsy strings select "Off". Any other value is treated
    // as an explicit state name (e.g. a specific radio export value).
    private static string ResolveButtonState(PdfDictionary field, string value, PdfDocumentCore core)
    {
        var v = value.Trim();
        var isTruthy = v is "true" or "on" or "yes" or "1" or "checked" or "x" or "X" or "On" or "Yes";
        var isFalsy = v.Length == 0 || v is "false" or "off" or "no" or "0" or "unchecked" or "Off" or "No";

        if (isFalsy && !isTruthy)
            return "Off";

        if (isTruthy)
            return OnStateName(field, core) ?? "Yes";

        // Explicit state name supplied by the caller.
        return v;
    }

    // Finds the "on" appearance-state name from the field's (or its widget kid's) /AP /N
    // dictionary — the first key that is not "Off".
    private static string? OnStateName(PdfDictionary field, PdfDocumentCore core)
    {
        var ap = ResolveDict(field["AP"], core);
        // For a parent field, the appearance lives on the widget kid.
        if (ap is null && field.Get<PdfArray>(PdfName.Kids) is { Count: > 0 } kids
                       && kids[0] is PdfIndirectReference kr
                       && core.ResolveIndirect(kr.ObjectNumber).Value is PdfDictionary kidDict)
            ap = ResolveDict(kidDict["AP"], core);

        var normal = ap is not null ? ResolveDict(ap["N"], core) : null;
        if (normal is null) return null;
        foreach (var (key, _) in normal.Entries)
        {
            if (!string.Equals(key, "Off", StringComparison.Ordinal))
                return key;
        }

        return null;
    }
}
