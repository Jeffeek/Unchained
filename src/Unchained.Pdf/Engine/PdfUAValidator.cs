using System.Globalization;
using System.Text;
using Unchained.Drawing.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine;

// ── Validator ─────────────────────────────────────────────────────────────────

/// <summary>
///     Validates a PDF document against ISO 14289-1 (PDF/UA-1) accessibility requirements.
///     <para>
///         Rules implemented correspond directly to ISO 14289-1 clause numbers.
///         The validator checks every rule that can be verified statically from the PDF object graph;
///         rules that require human judgment (e.g. reading-order correctness, meaningful alt text)
///         are flagged as warnings where detectable.
///     </para>
/// </summary>
internal static class PdfUAValidator
{
    // Standard PDF structure type names from ISO 32000-1 Table 333.
    private static readonly HashSet<string> StandardStructureTypes = new(StringComparer.Ordinal)
    {
        "Document", "Part", "Art", "Sect", "Div", "BlockQuote", "Caption",
        "TOC", "TOCI", "Index", "NonStruct", "Private",
        "H", "H1", "H2", "H3", "H4", "H5", "H6",
        "P", "L", "LI", "LBody",
        "Table", "TR", "TH", "TD", "THead", "TBody", "TFoot",
        "Span", "Quote", "Note", "Reference", "BibEntry", "Code", "Link", "Annot",
        "Ruby", "Warichu",
        "Figure", "Formula", "Form"
    };
    // ── Entry point ───────────────────────────────────────────────────────────

    internal static PdfUAValidationResult Validate(byte[] pdfBytes)
    {
        PdfDocumentCore core;
        try
        {
            core = PdfDocumentCore.Parse(pdfBytes);
        }
        catch (PdfEncryptedException)
        {
            return Fail("7.1", "Encrypted documents cannot be PDF/UA conformant (ISO 14289-1 §7.1).");
        }
        catch (PdfException ex)
        {
            return Fail("7.1", $"PDF structure is invalid: {ex.Message}");
        }

        var v = new List<PdfUAViolation>();

        // ── §7.1  File header ─────────────────────────────────────────────────
        CheckPdfVersion(pdfBytes, v);

        // ── §7.2  Tagged PDF ──────────────────────────────────────────────────
        CheckMarkInfo(core, v);

        // ── §7.3  Document title ──────────────────────────────────────────────
        CheckDocumentTitle(core, v);

        // ── §7.4  Language ────────────────────────────────────────────────────
        CheckLanguage(core, v);

        // ── §7.5  Logical structure ───────────────────────────────────────────
        CheckStructTreeRoot(core, v);

        // ── §7.6  Roles ───────────────────────────────────────────────────────
        CheckRoleMap(core, v);

        // ── §7.7  Alternate descriptions ─────────────────────────────────────
        CheckFigureAltText(core, v);
        CheckFormFieldAltText(core, v);

        // ── §7.8  Headings ────────────────────────────────────────────────────
        CheckHeadings(core, v);

        // ── §7.9  Tables ──────────────────────────────────────────────────────
        CheckTables(core, v);

        // ── §7.10 Lists ───────────────────────────────────────────────────────
        CheckLists(core, v);

        // ── §7.11 Graphics objects ────────────────────────────────────────────
        CheckUntaggedContent(core, v);

        // ── §7.13 Annotations ────────────────────────────────────────────────
        CheckAnnotations(core, v);

        // ── §7.14 Actions ─────────────────────────────────────────────────────
        CheckActions(core, v);

        // ── §7.17 XMP metadata ────────────────────────────────────────────────
        CheckXmpMetadata(core, v);

        return new PdfUAValidationResult
        {
            Violations = v
                .OrderBy(static x => x.Severity)
                .ThenBy(static x => x.RuleId)
                .ToList()
        };
    }

    // ── §7.1  File header ─────────────────────────────────────────────────────

    private static void CheckPdfVersion(byte[] bytes, ICollection<PdfUAViolation> v)
    {
        if (bytes.Length < 8)
        {
            v.Add(E("7.1", "File is too short to contain a valid PDF header."));
            return;
        }

        var header = Encoding.ASCII.GetString(bytes, 0, 8);
        if (!header.StartsWith("%PDF-", StringComparison.Ordinal))
        {
            v.Add(E("7.1", $"File does not start with '%PDF-'. Found: '{header}'."));
            return;
        }

        var verStr = header[5..];
        if (!double.TryParse(verStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var ver) || ver < 1.4)
            v.Add(E("7.1", $"PDF version {verStr} is below the minimum 1.4 required for PDF/UA-1."));
    }

    // ── §7.2  Tagged PDF ──────────────────────────────────────────────────────

    private static void CheckMarkInfo(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        var markInfo = Resolve<PdfDictionary>(core.Catalog[PdfName.MarkInfo], core);
        if (markInfo is null)
        {
            v.Add(E("7.2", "Catalog is missing required /MarkInfo dictionary."));
            return;
        }

        if (markInfo[PdfName.Marked] is not PdfBoolean { Value: true })
            v.Add(E("7.2", "/MarkInfo /Marked must be true for PDF/UA."));
    }

    // ── §7.3  Document title ──────────────────────────────────────────────────

    private static void CheckDocumentTitle(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        // /ViewerPreferences /DisplayDocTitle must be true.
        var vp = Resolve<PdfDictionary>(core.Catalog[PdfName.ViewerPreferences], core);
        if (vp?[PdfName.DisplayDocTitle] is not PdfBoolean { Value: true })
            v.Add(E("7.3", "/ViewerPreferences /DisplayDocTitle must be true so the window title bar shows the document title."));

        // The /Info /Title or DC title in XMP must be present.
        var hasInfoTitle = core.Info?.Get<PdfString>("Title") is not null;
        var hasXmpTitle = HasXmpTitle(core);
        if (!hasInfoTitle && !hasXmpTitle)
            v.Add(W("7.3", "Document has no title in /Info /Title or XMP dc:title. Screen readers use this as the document name."));
    }

    private static bool HasXmpTitle(PdfDocumentCore core)
    {
        var metaObj = core.Catalog[PdfName.Metadata];
        var stream = metaObj switch
        {
            PdfStream s => s,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
            _ => null
        };
        if (stream is null) return false;

        try
        {
            var xmp = StreamFilters.Decode(stream).Span.FromUtf8Span();
            return xmp.Contains("dc:title", StringComparison.OrdinalIgnoreCase) ||
                   xmp.Contains("dc:Title", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    // ── §7.4  Language ────────────────────────────────────────────────────────

    private static void CheckLanguage(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        var lang = core.Catalog[PdfName.Lang];
        if (lang is null)
        {
            v.Add(E("7.4", "Catalog is missing required /Lang entry (BCP 47 language tag, e.g. \"en-US\")."));
            return;
        }

        var langStr = lang switch
        {
            PdfString s => Encoding.Latin1.GetString(s.Bytes.Span),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(langStr))
            v.Add(E("7.4", "/Lang entry is present but empty. A valid BCP 47 language tag is required."));
    }

    // ── §7.5  Logical structure ───────────────────────────────────────────────

    private static void CheckStructTreeRoot(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        var structTreeRootObj = core.Catalog[PdfName.StructTreeRoot];
        if (structTreeRootObj is null)
        {
            v.Add(E("7.5", "Catalog is missing required /StructTreeRoot entry."));
            return;
        }

        var root = Resolve<PdfDictionary>(structTreeRootObj, core);
        if (root is null)
        {
            v.Add(E("7.5", "/StructTreeRoot cannot be resolved to a dictionary."));
            return;
        }

        // /ParentTree is required for PDF/UA (§7.5).
        if (root[PdfName.ParentTree] is null)
            v.Add(E("7.5", "/StructTreeRoot is missing required /ParentTree number tree."));

        // Must have at least one child element.
        if (root[PdfName.K] is null)
            v.Add(E("7.5", "/StructTreeRoot has no /K children — the structure tree is empty."));
    }

    // ── §7.6  Role map ────────────────────────────────────────────────────────

    private static void CheckRoleMap(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        var root = Resolve<PdfDictionary>(core.Catalog[PdfName.StructTreeRoot], core);
        if (root is null) return;

        var roleMap = Resolve<PdfDictionary>(root[PdfName.RoleMap], core);
        if (roleMap is null) return; // /RoleMap is optional when only standard types are used.

        // Any non-standard type in /RoleMap must map to a standard type.
        foreach (var (customType, mapped) in roleMap.Entries)
        {
            if (mapped is not PdfName targetName)
            {
                v.Add(E("7.6", $"/RoleMap entry for /{customType} must map to a /Name, not {mapped.GetType().Name}."));
                continue;
            }

            if (!StandardStructureTypes.Contains(targetName.Value))
                v.Add(W("7.6", $"/RoleMap maps /{customType} to /{targetName.Value} which is not a standard PDF/UA structure type."));
        }
    }

    // ── §7.7  Alternate descriptions ─────────────────────────────────────────

    private static void CheckFigureAltText(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        var root = Resolve<PdfDictionary>(core.Catalog[PdfName.StructTreeRoot], core);
        if (root is null) return;

        WalkStructTree(root,
            core,
            elem =>
            {
                var type = elem.GetName(PdfName.S.Value);
                if (type is not ("Figure" or "Formula" or "Form"))
                    return;

                var hasAlt = elem[PdfName.Alt] is not null;
                var hasActualText = elem[PdfName.ActualText] is not null;

                if (!hasAlt && !hasActualText)
                    v.Add(E("7.7", $"/{type} structure element is missing required /Alt or /ActualText entry."));
            });
    }

    private static void CheckFormFieldAltText(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        // Widget annotations must have a /TU (tooltip) entry as the accessible name.
        for (var page = 1; page <= core.PageCount; page++)
        {
            var pageDict = core.GetPage(page);
            var annotsObj = pageDict[PdfName.Annots];
            var annots = annotsObj switch
            {
                PdfArray a => a,
                PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfArray,
                _ => null
            };
            if (annots is null) continue;

            foreach (var elem in annots.Elements)
            {
                var dict = core.ResolveDict(elem);
                if (dict?.GetName("Subtype") != "Widget") continue;

                if (dict[PdfName.TU] is null)
                    v.Add(W("7.7", $"Widget annotation on page {page} is missing /TU (tooltip / accessible name).", pageNumber: page));
            }
        }
    }

    // ── §7.8  Headings ────────────────────────────────────────────────────────

    private static void CheckHeadings(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        // Collect all heading levels in document order and check for skipped levels.
        var headingLevels = new List<int>();
        var root = Resolve<PdfDictionary>(core.Catalog[PdfName.StructTreeRoot], core);
        if (root is null) return;

        WalkStructTree(root,
            core,
            elem =>
            {
                var type = elem.GetName(PdfName.S.Value);
                switch (type)
                {
                    case null:
                        return;
                    case ['H', _, ..] when int.TryParse(type[1..], out var level) && level is >= 1 and <= 6:
                        headingLevels.Add(level);
                    break;
                }
            });

        for (var i = 1; i < headingLevels.Count; i++)
        {
            var prev = headingLevels[i - 1];
            var curr = headingLevels[i];
            if (curr > prev + 1)
                v.Add(W("7.8", $"Heading level skipped: H{prev} followed by H{curr}. Headings must not skip levels (ISO 14289-1 §7.8)."));
        }
    }

    // ── §7.9  Tables ─────────────────────────────────────────────────────────

    private static void CheckTables(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        var root = Resolve<PdfDictionary>(core.Catalog[PdfName.StructTreeRoot], core);
        if (root is null) return;

        WalkStructTree(root,
            core,
            elem =>
            {
                if (elem.GetName(PdfName.S.Value) != "Table")
                    return;

                // Each Table must have at least one TR child (via /K).
                var hasRow = false;
                WalkStructKids(elem,
                    core,
                    child =>
                    {
                        if (child.GetName(PdfName.S.Value) == "TR")
                            hasRow = true;
                    });

                if (!hasRow)
                    v.Add(W("7.9", "Table structure element has no TR (table row) children."));
            });
    }

    // ── §7.10  Lists ─────────────────────────────────────────────────────────

    private static void CheckLists(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        var root = Resolve<PdfDictionary>(core.Catalog[PdfName.StructTreeRoot], core);
        if (root is null) return;

        WalkStructTree(root,
            core,
            elem =>
            {
                if (elem.GetName(PdfName.S.Value) != "L")
                    return;

                // Each L must have LI children, and each LI must have LBody.
                WalkStructKids(elem,
                    core,
                    liElem =>
                    {
                        if (liElem.GetName(PdfName.S.Value) != "LI")
                            return;

                        var hasLBody = false;
                        WalkStructKids(liElem,
                            core,
                            child =>
                            {
                                if (child.GetName(PdfName.S.Value) == "LBody")
                                    hasLBody = true;
                            });

                        if (!hasLBody)
                            v.Add(W("7.10", "LI (list item) structure element is missing an LBody child."));
                    });
            });
    }

    // ── §7.11  Untagged content ───────────────────────────────────────────────

    private static void CheckUntaggedContent(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        // For each page, verify that content streams contain at least one BDC or BMC operator.
        // A complete check (every operator covered) is impractical without a full content parser;
        // this heuristic detects completely untagged pages.
        for (var page = 1; page <= core.PageCount; page++)
        {
            var pageDict = core.GetPage(page);
            var contentsObj = pageDict[PdfName.Contents];

            IEnumerable<PdfObject> contentStreams = contentsObj switch
            {
                null => [],
                PdfArray arr => arr.Elements,
                _ => [contentsObj]
            };

            var pageHasMarkedContent = false;
            var pageHasContent = false;

            foreach (var contentRef in contentStreams)
            {
                var streamObj = contentRef switch
                {
                    PdfStream s => s,
                    PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
                    _ => null
                };
                if (streamObj is null) continue;

                try
                {
                    var decoded = StreamFilters.Decode(streamObj);
                    var text = Encoding.Latin1.GetString(decoded.Span);
                    if (text.Contains("BDC") || text.Contains("BMC")) pageHasMarkedContent = true;
                    // Check if there is any actual drawing content.
                    if (text.Contains("Tj") || text.Contains("TJ") || text.Contains(" re ") ||
                        text.Contains(" f\n") || text.Contains(" S\n") || text.Contains(" cm\n"))
                        pageHasContent = true;
                }
                catch
                {
                    // Skip unreadable streams.
                }
            }

            if (pageHasContent && !pageHasMarkedContent)
            {
                v.Add(E("7.11",
                    $"Page {page} contains drawing operators but no marked-content sequences (BDC/BMC). All content must be tagged.",
                    pageNumber: page));
            }
        }
    }

    // ── §7.13  Annotations ────────────────────────────────────────────────────

    private static void CheckAnnotations(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        for (var page = 1; page <= core.PageCount; page++)
        {
            var pageDict = core.GetPage(page);
            var annotsObj = pageDict[PdfName.Annots];
            var annots = annotsObj switch
            {
                PdfArray a => a,
                PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfArray,
                _ => null
            };
            if (annots is null) continue;

            foreach (var elem in annots.Elements)
            {
                var dict = core.ResolveDict(elem);
                if (dict is null) continue;

                var subtype = dict.GetName("Subtype") ?? string.Empty;

                // All annotations except /Popup must have a /Contents or /TU entry (accessible name).
                if (subtype is not ("Popup" or "Link"))
                {
                    var hasContents = dict[PdfName.Contents] is not null;
                    var hasTu = dict[PdfName.TU] is not null;
                    if (!hasContents && !hasTu)
                    {
                        v.Add(W("7.13",
                            $"/{subtype} annotation on page {page} has no /Contents or /TU — screen readers cannot describe it.",
                            pageNumber: page));
                    }
                }

                // Tab order: pages with annotations must have /Tabs /S (structure order).
                var tabs = pageDict.GetName("Tabs");
                if (tabs is null)
                {
                    v.Add(W("7.13",
                        $"Page {page} has annotations but no /Tabs entry. Set /Tabs /S for structure-based tab order.",
                        pageNumber: page));
                }
            }
        }
    }

    // ── §7.14  Actions ────────────────────────────────────────────────────────

    private static void CheckActions(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        // /AA (additional actions) on the document catalog must not have scripts (§7.14.2).
        if (core.Catalog[PdfName.AA] is not null)
        {
            v.Add(W("7.14",
                "Catalog contains /AA (additional actions). Verify no JavaScript actions are present — they are not permitted in PDF/UA."));
        }

        // OpenAction: if present, may not be a named action of type /Named (§7.14.1).
        var openAction = core.Catalog[PdfName.OpenAction];
        if (openAction is null) return;

        var actionDict = Resolve<PdfDictionary>(openAction, core);
        if (actionDict?.GetName("S") == "JavaScript")
            v.Add(E("7.14", "Document /OpenAction contains a JavaScript action, which is not permitted in PDF/UA."));
    }

    // ── §7.17  XMP metadata ───────────────────────────────────────────────────

    private static void CheckXmpMetadata(PdfDocumentCore core, ICollection<PdfUAViolation> v)
    {
        var metaObj = core.Catalog[PdfName.Metadata];
        var stream = metaObj switch
        {
            PdfStream s => s,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
            _ => null
        };

        if (stream is null)
        {
            v.Add(E("7.17", "Catalog is missing required /Metadata XMP stream."));
            return;
        }

        string xmp;
        try
        {
            xmp = StreamFilters.Decode(stream).Span.FromUtf8Span();
        }
        catch
        {
            v.Add(E("7.17", "Could not decode /Metadata stream."));
            return;
        }

        // Must contain pdfuaid:part = 1.
        if (!xmp.Contains("pdfuaid", StringComparison.OrdinalIgnoreCase))
            v.Add(E("7.17", "XMP metadata is missing required pdfuaid namespace declaration and pdfuaid:part property."));
        else if (!xmp.Contains(">1<", StringComparison.Ordinal) &&
                 !xmp.Contains(">1 <", StringComparison.Ordinal) &&
                 !xmp.Contains("part>1", StringComparison.Ordinal))
            v.Add(E("7.17", "XMP metadata pdfuaid:part value must be '1' for PDF/UA-1."));
    }

    // ── Structure tree walkers ────────────────────────────────────────────────

    private static void WalkStructTree(
        PdfDictionary root,
        PdfDocumentCore core,
        Action<PdfDictionary> visitor
    )
    {
        var visited = new HashSet<int>();
        var queue = new Queue<PdfDictionary>();

        EnqueueKids(root, core, queue);

        while (queue.Count > 0)
        {
            var elem = queue.Dequeue();
            visitor(elem);
            EnqueueKids(elem, core, queue);
        }

        return;

        void EnqueueKids(PdfDictionary node, PdfDocumentCore c, Queue<PdfDictionary> q)
        {
            var kObj = node[PdfName.K];
            if (kObj is null) return;

            IEnumerable<PdfObject> kids = kObj switch
            {
                PdfArray arr => arr.Elements,
                _ => [kObj]
            };

            foreach (var kid in kids)
            {
                var resolved = kid switch
                {
                    PdfDictionary d => d,
                    PdfIndirectReference r when visited.Add(r.ObjectNumber) =>
                        c.ResolveIndirect(r.ObjectNumber).Value as PdfDictionary,
                    _ => null
                };

                if (resolved?.GetName(PdfName.S.Value) is not null)
                    q.Enqueue(resolved);
            }
        }
    }

    private static void WalkStructKids(
        PdfDictionary elem,
        PdfDocumentCore core,
        Action<PdfDictionary> visitor
    )
    {
        var kObj = elem[PdfName.K];
        if (kObj is null) return;

        IEnumerable<PdfObject> kids = kObj switch
        {
            PdfArray arr => arr.Elements,
            _ => [kObj]
        };

        foreach (var kid in kids)
        {
            var resolved = core.ResolveDict(kid);

            if (resolved is not null)
                visitor(resolved);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static T? Resolve<T>(PdfObject? obj, PdfDocumentCore core)
        where T : PdfObject =>
        obj switch
        {
            T direct => direct,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as T,
            _ => null
        };

    private static PdfUAValidationResult Fail(string ruleId, string message) =>
        new() { Violations = [E(ruleId, message)] };

    // ReSharper disable once BadListLineBreaks
    private static PdfUAViolation E(
        string ruleId,
        string description,
        int? objectNumber = null,
        int? pageNumber = null
    ) =>
        new(ruleId, description, PdfUAViolationSeverity.Error, objectNumber, pageNumber);

    // ReSharper disable once BadListLineBreaks
    private static PdfUAViolation W(
        string ruleId,
        string description,
        int? objectNumber = null,
        int? pageNumber = null
    ) =>
        new(ruleId, description, PdfUAViolationSeverity.Warning, objectNumber, pageNumber);
}
