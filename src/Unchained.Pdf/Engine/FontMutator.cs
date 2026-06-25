using Unchained.Drawing.Primitives.Fonts;
using Unchained.Pdf.Core;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Font-program mutations on a loaded document: embedding caller-supplied bytes for
///     Standard 14 fonts, replacing an embedded font, and subsetting embedded fonts to the
///     glyphs actually used. Extracted from <see cref="DocumentProcessor" />; all rewrites go
///     through <see cref="MutationHelper.SerializeAndReplace" />.
/// </summary>
internal static class FontMutator
{
    internal static void EmbedStandardFonts(
        PdfDocumentAdapter adapter,
        IReadOnlyDictionary<string, byte[]> fontMap
    )
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var changed = false;

        for (var i = 0; i < existing.Count; i++)
        {
            var obj = existing[i];
            var dict = obj.Value as PdfDictionary;
            if (dict is null) continue;
            if (dict.GetName("Type") != "Font") continue;

            var baseFont = dict.GetName(PdfName.BaseFont.Value);
            if (baseFont is null) continue;

            // Strip style suffixes to find the base family name.
            var family = NormalizeBaseFont(baseFont);
            if (!fontMap.TryGetValue(family, out var fontBytes) &&
                !fontMap.TryGetValue(baseFont, out fontBytes))
                continue;

            // Check if already embedded.
            var descriptor = dict.Get<PdfDictionary>("FontDescriptor") ??
                             (dict[PdfName.FontDescriptor] is PdfIndirectReference fd
                                 ? adapter.Core.ResolveIndirect(fd.ObjectNumber).Value as PdfDictionary
                                 : null);

            if (descriptor is not null &&
                (descriptor[PdfName.FontFile] is not null ||
                 descriptor[PdfName.FontFile2] is not null ||
                 descriptor[PdfName.FontFile3] is not null))
                continue; // Already embedded.

            // Build /FontDescriptor with /FontFile2 (TrueType).
            // Read actual metrics from the font file; fall back to conservative defaults.
            var metrics = TrueTypeMetrics.Read(fontBytes) ?? TrueTypeMetrics.HelveticaFallback;

            var maxObj = existing.Max(static o => o.ObjectNumber);
            var descObjNum = AppendFontDescriptor(existing, ref maxObj, baseFont, fontBytes, metrics);

            // Update the font dict to include the descriptor.
            var updatedEntries = new Dictionary<string, PdfObject>(dict.Entries)
            {
                ["FontDescriptor"] = new PdfIndirectReference(descObjNum, 0)
            };
            existing[i] = new PdfIndirectObject(obj.ObjectNumber, obj.Generation, new PdfDictionary(updatedEntries));
            changed = true;
        }

        if (changed)
            MutationHelper.SerializeAndReplace(adapter, existing);
    }

    internal static void ReplaceFont(
        PdfDocumentAdapter adapter,
        string fontName,
        byte[] newFontBytes
    )
    {
        var existing = adapter.Core.CollectObjects().ToList();
        var metrics = TrueTypeMetrics.Read(newFontBytes) ?? TrueTypeMetrics.HelveticaFallback;
        var changed = false;
        var normalised = NormalizeBaseFont(fontName);
        var maxObj = existing.Max(static o => o.ObjectNumber);

        for (var i = 0; i < existing.Count; i++)
        {
            var obj = existing[i];
            var dict = obj.Value as PdfDictionary;
            if (dict is null) continue;
            if (dict.GetName("Type") != "Font") continue;

            var baseFont = dict.GetName(PdfName.BaseFont.Value);
            if (baseFont is null) continue;
            if (!string.Equals(NormalizeBaseFont(baseFont), normalised, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(baseFont, fontName, StringComparison.OrdinalIgnoreCase))
                continue;

            // Build new /FontFile2 stream + /FontDescriptor.
            var descObjNum = AppendFontDescriptor(existing, ref maxObj, baseFont, newFontBytes, metrics);

            // Update the font dictionary with the new descriptor.
            var updatedEntries = new Dictionary<string, PdfObject>(dict.Entries)
            {
                ["FontDescriptor"] = new PdfIndirectReference(descObjNum, 0)
            };
            existing[i] = new PdfIndirectObject(
                obj.ObjectNumber,
                obj.Generation,
                new PdfDictionary(updatedEntries)
            );
            changed = true;
        }

        if (changed)
            MutationHelper.SerializeAndReplace(adapter, existing);
    }

    internal static void SubsetFonts(PdfDocumentAdapter adapter)
    {
        var existing = adapter.Core.CollectObjects().ToList();

        // Step 1: collect used glyph IDs per FontFile object number across all pages.
        // Key = /FontFile2 stream object number, Value = set of used glyph IDs.
        var usedGlyphs = new Dictionary<int, HashSet<int>>();
        CollectUsedGlyphs(adapter, existing, usedGlyphs);
        if (usedGlyphs.Count == 0) return;

        // Step 2: subset each embedded font stream.
        var changed = false;
        for (var i = 0; i < existing.Count; i++)
        {
            var obj = existing[i];
            if (!usedGlyphs.TryGetValue(obj.ObjectNumber, out var glyphs)) continue;
            if (obj.Value is not PdfStream fontStream) continue;

            var originalBytes = fontStream.Data.ToArray();
            if (originalBytes.Length == 0) continue;

            var subsetBytes = TrueTypeSubsetter.Subset(originalBytes, glyphs);
            if (subsetBytes.Length >= originalBytes.Length) continue; // no savings

            // Rebuild the font stream with updated length.
            var newDict = new PdfDictionary(
                new Dictionary<string, PdfObject>(
                    fontStream.Dictionary.Entries
                )
                {
                    [PdfName.Length.Value] = new PdfInteger(subsetBytes.Length),
                    ["Length1"] = new PdfInteger(subsetBytes.Length)
                }
            );
            existing[i] = new PdfIndirectObject(
                obj.ObjectNumber,
                obj.Generation,
                new PdfStream(newDict, subsetBytes)
            );
            changed = true;
        }

        if (changed)
            MutationHelper.SerializeAndReplace(adapter, existing);
    }

    private static string NormalizeBaseFont(string baseFont)
    {
        // "Helvetica-Bold" → "Helvetica", "Times-Roman" → "Times-Roman" (keep as-is for serif)
        var dash = baseFont.IndexOf('-');
        return dash > 0 ? baseFont[..dash] : baseFont;
    }

    // Appends a /FontFile2 stream and a /FontDescriptor referencing it to the object list,
    // advancing maxObj for each. Returns the /FontDescriptor object number so the caller can
    // point its font dictionary at it. Shared by EmbedStandardFonts and ReplaceFont.
    private static int AppendFontDescriptor(
        ICollection<PdfIndirectObject> existing,
        ref int maxObj,
        string baseFont,
        byte[] fontBytes,
        FontMetrics metrics
    )
    {
        var fontFileObjNum = ++maxObj;
        var fontFileDict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                [PdfName.Length.Value] = new PdfInteger(fontBytes.Length),
                ["Length1"] = new PdfInteger(fontBytes.Length)
            }
        );
        existing.Add(new PdfIndirectObject(fontFileObjNum, 0, new PdfStream(fontFileDict, fontBytes)));

        var descObjNum = ++maxObj;
        var descEntries = new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.FontDescriptor,
            ["FontName"] = PdfName.Get(baseFont),
            ["Flags"] = new PdfInteger(32),
            ["FontBBox"] = new PdfArray(
                [
                    new PdfInteger(metrics.XMin), new PdfInteger(metrics.YMin),
                    new PdfInteger(metrics.XMax), new PdfInteger(metrics.YMax)
                ]
            ),
            ["ItalicAngle"] = new PdfInteger(0),
            ["Ascent"] = new PdfInteger(metrics.Ascent),
            ["Descent"] = new PdfInteger(metrics.Descent),
            ["CapHeight"] = new PdfInteger(metrics.CapHeight),
            ["StemV"] = new PdfInteger(metrics.StemV),
            ["FontFile2"] = new PdfIndirectReference(fontFileObjNum, 0)
        };
        existing.Add(new PdfIndirectObject(descObjNum, 0, new PdfDictionary(descEntries)));

        return descObjNum;
    }

    // Walks all pages' content streams and collects glyph IDs for each embedded font.
    // usedGlyphs: key = FontFile2 stream object number, value = set of glyph IDs used.
    private static void CollectUsedGlyphs(
        PdfDocumentAdapter adapter,
        IEnumerable<PdfIndirectObject> objects,
        IDictionary<int, HashSet<int>> usedGlyphs
    )
    {
        // Build a map from FontDescriptor object number → FontFile2 object number.
        var descToFontFile = new Dictionary<int, int>();
        // Build a map from Font dict object number → FontFile2 object number.
        var fontToFontFile = new Dictionary<int, int>();

        foreach (var obj in objects.Where(static o => o.Value is PdfDictionary))
        {
            var dict = (PdfDictionary)obj.Value;

            var type = dict.GetName("Type");
            switch (type)
            {
                case "FontDescriptor":
                {
                    if (dict[PdfName.FontFile2] is PdfIndirectReference ff2)
                        descToFontFile[obj.ObjectNumber] = ff2.ObjectNumber;
                    break;
                }
                case "Font":
                {
                    // Link Font → FontDescriptor → FontFile2.
                    if (dict[PdfName.FontDescriptor] is not PdfIndirectReference fdRef)
                        continue;
                    if (!descToFontFile.TryGetValue(fdRef.ObjectNumber, out var ffNum))
                        continue;

                    fontToFontFile[obj.ObjectNumber] = ffNum;
                    break;
                }
            }
        }

        if (fontToFontFile.Count == 0) return;

        // Walk each page's content operators to collect glyph IDs.
        for (var p = 1; p <= adapter.Core.PageCount; p++)
        {
            var pageDict = adapter.Core.GetPage(p);
            var pageAdapter = new PdfPageAdapter(pageDict, p, adapter.Core);
            var ops = pageAdapter.GetContentOperators();
            var compFonts = pageAdapter.GetCompositeFonts(); // resource name → composite info

            // Walk the font resources to find object numbers for the resource names.
            var resources = pageDict[PdfName.Resources];
            var resDict = adapter.Core.ResolveDict(resources);
            var fontResDict = resDict?[PdfName.Font] as PdfDictionary
                              ?? (resDict?[PdfName.Font] is PdfIndirectReference fr
                                  ? adapter.Core.ResolveIndirect(fr.ObjectNumber).Value as PdfDictionary
                                  : null);
            if (fontResDict is null) continue;

            // Map resource name → FontFile2 object number.
            var resNameToFontFile = new Dictionary<string, int>();
            foreach (var (resName, fontObj) in fontResDict.Entries)
            {
                var fontObjNum = fontObj is PdfIndirectReference fontRef
                    ? fontRef.ObjectNumber
                    : -1;
                if (fontObjNum > 0 && fontToFontFile.TryGetValue(fontObjNum, out var ffNum))
                    resNameToFontFile[resName] = ffNum;
            }

            if (resNameToFontFile.Count == 0) continue;

            // ReSharper disable once GrammarMistakeInComment
            // Walk operators: Tf sets current font, Tj/TJ/'/" show strings.
            var currentFontRes = string.Empty;
            foreach (var op in ops)
            {
                switch (op.Name)
                {
                    case "Tf" when op.Operands.Count >= 1:
                        currentFontRes = (op.Operands[0] as PdfName)?.Value ?? string.Empty;
                    break;
                    case "Tj" when op.Operands.Count >= 1:
                    {
                        if (!resNameToFontFile.TryGetValue(currentFontRes, out var ff))
                            break;

                        if (!usedGlyphs.TryGetValue(ff, out var gs))
                            usedGlyphs[ff] = gs = [];

                        CollectGlyphsFromString(op.Operands[0], currentFontRes, compFonts, gs);
                        break;
                    }
                    case "TJ" when op.Operands is [PdfArray arr, ..]:
                    {
                        if (!resNameToFontFile.TryGetValue(currentFontRes, out var ff))
                            break;

                        if (!usedGlyphs.TryGetValue(ff, out var gs))
                            usedGlyphs[ff] = gs = [];

                        foreach (var elem in arr.Elements)
                            CollectGlyphsFromString(elem, currentFontRes, compFonts, gs);
                        break;
                    }
                }
            }
        }
    }

    // Extracts glyph IDs from a PdfString operand (simple or composite font).
    private static void CollectGlyphsFromString(
        PdfObject obj,
        string fontResName,
        IReadOnlyDictionary<string, CompositeFontInfo> compFonts,
        ISet<int> result
    )
    {
        if (obj is not PdfString ps)
            return;

        var bytes = ps.GetBinaryBytes();

        if (compFonts.TryGetValue(fontResName, out var cfi) && cfi.IdentityEncoding)
        {
            // Type0/CID: 2-byte CID pairs → glyph IDs via CIDToGIDMap.
            var span = bytes.Span;
            for (var i = 0; i + 1 < span.Length; i += 2)
            {
                var cid = (span[i] << 8) | span[i + 1];
                var gid = cfi.IdentityCidToGid
                    ? cid
                    : cfi.CidToGid is not null && cid < cfi.CidToGid.Count
                        ? cfi.CidToGid[cid]
                        : cid;
                result.Add(gid);
            }
        }
        else
        {
            // Simple font: each byte is a character code = approximate glyph ID.
            foreach (var b in bytes.Span)
                result.Add(b);
        }
    }
}
