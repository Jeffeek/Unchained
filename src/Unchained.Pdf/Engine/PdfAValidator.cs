using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Unchained.Drawing.Primitives.Extensions;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing.Filters;

namespace Unchained.Pdf.Engine;

/// <summary>
///     Validates a PDF document against ISO 19005 PDF/A conformance profiles.
///     Rule IDs correspond to ISO 19005-1 (PDF/A-1) clause numbers.
/// </summary>
internal static class PdfAValidator
{
    // ── §6.5 Annotations ─────────────────────────────────────────────────────

    private static readonly HashSet<string> ProhibitedAnnotationTypes = new(StringComparer.Ordinal)
    {
        "Sound", "Movie", "Screen", "FileAttachment", "3D"
    };

    // ── §6.6 Actions ──────────────────────────────────────────────────────────

    private static readonly HashSet<string> ProhibitedActionTypes = new(StringComparer.Ordinal)
    {
        "Launch", "Sound", "Movie", "ResetForm", "ImportData",
        "JavaScript", "SetOCGState", "Trans", "GoTo3DView"
    };
    // ── Entry point ───────────────────────────────────────────────────────────

    internal static PdfAValidationResult Validate(byte[] pdfBytes, PdfAProfile profile)
    {
        PdfDocumentCore core;
        try
        {
            core = PdfDocumentCore.Parse(pdfBytes);
        }
        catch (PdfEncryptedException)
        {
            // Encryption is itself a PDF/A-1b violation (§6.1.3).
            // Return immediately with that single violation.
            return new PdfAValidationResult
            {
                Profile = profile,
                Violations = [E("6.1.3", "Encrypted documents are not permitted in PDF/A.")]
            };
        }
        catch (PdfException ex)
        {
            return new PdfAValidationResult
            {
                Profile = profile,
                Violations = [E("6.1", $"PDF structure is invalid and could not be parsed: {ex.Message}")]
            };
        }

        var v = new List<PdfAViolation>();

        // ── §6.1 File structure ───────────────────────────────────────────────
        CheckPdfVersion(pdfBytes, profile, v);
        CheckEncryption(core, v);
        CheckFileId(core, v);
        CheckLzwFilters(core, v);

        // ── §6.2 Graphics ─────────────────────────────────────────────────────
        CheckOutputIntent(core, v);
        CheckTransparency(core, v);

        // ── §6.3 Fonts ────────────────────────────────────────────────────────
        CheckFontsEmbedded(core, v);

        // ── §6.5 Annotations ─────────────────────────────────────────────────
        CheckAnnotations(core, v);

        // ── §6.6 Actions ──────────────────────────────────────────────────────
        CheckActions(core, v);

        // ── §6.7 Metadata ─────────────────────────────────────────────────────
        CheckXmpMetadata(core, profile, v);

        // ── §6.8 Optional / embedded content ─────────────────────────────────
        CheckEmbeddedFiles(core, v);
        CheckCollection(core, v);

        return new PdfAValidationResult
        {
            Profile = profile,
            Violations = v
                .OrderBy(static x => x.Severity)
                .ThenBy(static x => x.RuleId)
                .ToList()
        };
    }

    // ── §6.1.2 File header ────────────────────────────────────────────────────

    private static void CheckPdfVersion(byte[] bytes, PdfAProfile profile, ICollection<PdfAViolation> v)
    {
        // First 8 bytes: %PDF-1.x
        if (bytes.Length < 8)
        {
            v.Add(E("6.1.2", "File is too short to contain a valid PDF header."));
            return;
        }

        var header = Encoding.ASCII.GetString(bytes, 0, 8);
        if (!header.StartsWith("%PDF-", StringComparison.Ordinal))
        {
            v.Add(E("6.1.2", $"File header does not start with '%PDF-'. Found: '{header[..5]}'."));
            return;
        }

        var verStr = header[5..];
        if (!double.TryParse(verStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var ver))
        {
            v.Add(E("6.1.2", $"Cannot parse PDF version from header: '{verStr}'."));
            return;
        }

        // PDF/A-1 requires PDF 1.0–1.4; PDF/A-2 requires 1.0–1.7
        var maxVer = profile is PdfAProfile.PdfA1B or PdfAProfile.PdfA1A ? 1.4 : 1.7;
        if (ver > maxVer)
            v.Add(E("6.1.2", $"PDF version {ver:F1} exceeds maximum {maxVer:F1} for {profile}."));
    }

    // ── §6.1.3 No encryption ──────────────────────────────────────────────────

    private static void CheckEncryption(PdfDocumentCore core, ICollection<PdfAViolation> v)
    {
        if (core.IsEncrypted)
            v.Add(E("6.1.3", "Encrypted documents are not permitted in PDF/A."));
    }

    // ── §6.1.3 File ID ────────────────────────────────────────────────────────

    private static void CheckFileId(PdfDocumentCore core, ICollection<PdfAViolation> v)
    {
        var id = core.Trailer.Get<PdfArray>(PdfName.ID);
        if (id is null || id.Count < 2)
            v.Add(E("6.1.3", "Trailer is missing required /ID array."));
    }

    // ── §6.1.10 LZWDecode not permitted ──────────────────────────────────────

    private static void CheckLzwFilters(PdfDocumentCore core, ICollection<PdfAViolation> v)
    {
        foreach (var obj in core.CollectObjects())
        {
            var dict = obj.Value switch
            {
                PdfStream s => s.Dictionary,
                PdfDictionary d => d,
                _ => null
            };

            var filter = dict?[PdfName.Filter];
            if (filter is null)
                continue;

            var names = filter switch
            {
                PdfName n => [n.Value],
                PdfArray a => a.Elements.OfType<PdfName>().Select(static n => n.Value).ToArray(),
                _ => []
            };

            if (names.Any(static n => n is "LZWDecode" or "LZW"))
                v.Add(E("6.1.10", "LZWDecode filter is not permitted in PDF/A.", obj.ObjectNumber));
        }
    }

    // ── §6.2.2 Output intent ──────────────────────────────────────────────────

    private static void CheckOutputIntent(PdfDocumentCore core, ICollection<PdfAViolation> v)
    {
        // /OutputIntents is required when device-dependent colour spaces are used.
        // Heuristic: check if catalog has /OutputIntents; flag absence as a warning.
        var hasOutputIntent = core.Catalog["OutputIntents"] is not null;
        if (!hasOutputIntent)
        {
            v.Add(
                W(
                    "6.2.2",
                    "Catalog does not have /OutputIntents. Device-independent colour spaces or an " +
                    "ICC-based output intent are required for PDF/A compliance."
                )
            );
        }
    }

    // ── §6.4 Transparency ─────────────────────────────────────────────────────

    private static void CheckTransparency(PdfDocumentCore core, ICollection<PdfAViolation> v)
    {
        foreach (var obj in core.CollectObjects())
        {
            var dict = obj.Value switch
            {
                PdfDictionary d => d,
                PdfStream s => s.Dictionary,
                _ => null
            };
            if (dict is null)
                continue;

            // Look for ExtGState dicts that have transparency settings
            var type = dict.GetName("Type");
            if (type != "ExtGState" && type is not null)
                continue; // skip non-ExtGState (unless no type)

            if (dict[PdfName.SMask] is not null and not PdfName { Value: "None" })
                v.Add(E("6.4", "Transparency /SMask is not permitted in PDF/A-1.", obj.ObjectNumber));

            var bm = dict.Get<PdfName>("BM");
            if (bm is not null && bm.Value is not ("Normal" or "Compatible"))
                v.Add(E("6.4", $"Blend mode /{bm.Value} is not permitted in PDF/A-1 (only /Normal or /Compatible).", obj.ObjectNumber));

            if (dict.Get<PdfReal>("CA") is { Value: < 1.0 } caStroke)
                v.Add(E("6.4", $"Stroke opacity (/CA = {caStroke.Value:F2}) must be 1.0 in PDF/A-1.", obj.ObjectNumber));

            if (dict.Get<PdfReal>("ca") is { Value: < 1.0 } caFill)
                v.Add(E("6.4", $"Fill opacity (/ca = {caFill.Value:F2}) must be 1.0 in PDF/A-1.", obj.ObjectNumber));
        }
    }

    // ── §6.3 Fonts embedded ───────────────────────────────────────────────────

    private static void CheckFontsEmbedded(PdfDocumentCore core, ICollection<PdfAViolation> v)
    {
        var seen = new HashSet<int>();
        foreach (var obj in core.CollectObjects())
        {
            var dict = obj.Value as PdfDictionary ?? (obj.Value as PdfStream)?.Dictionary;
            if (dict is null)
                continue;
            if (dict.GetName("Type") != "Font")
                continue;
            if (!seen.Add(obj.ObjectNumber))
                continue;

            var subtype = dict.GetName("Subtype");
            if (subtype is "Type0")
                continue; // Composite font — check descendant fonts below

            // Standard 14 fonts MUST be embedded in PDF/A-1
            var baseName = dict.GetName("BaseFont");

            var descriptor = Resolve<PdfDictionary>(dict[PdfName.FontDescriptor], core);
            if (descriptor is null)
            {
                if (subtype is not ("Type3" or "Type0"))
                    v.Add(E("6.3.3", $"Font '{baseName ?? "(unnamed)"}' (obj {obj.ObjectNumber}) is missing /FontDescriptor.", obj.ObjectNumber));

                continue;
            }

            var hasFile = descriptor[PdfName.FontFile] is not null ||
                          descriptor[PdfName.FontFile2] is not null ||
                          descriptor[PdfName.FontFile3] is not null;

            if (!hasFile)
            {
                v.Add(
                    E(
                        "6.3.3",
                        $"Font '{baseName ?? "(unnamed)"}' (obj {obj.ObjectNumber}) is not embedded. " +
                        "All fonts — including Standard 14 — must be embedded in PDF/A-1.",
                        obj.ObjectNumber
                    )
                );
            }
        }
    }

    private static void CheckAnnotations(PdfDocumentCore core, ICollection<PdfAViolation> v)
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

            if (annots is null)
                continue;

            foreach (var elem in annots.Elements)
            {
                var dict = core.ResolveDict(elem);

                if (dict is null)
                    continue;

                var subtype = dict.GetName("Subtype") ?? string.Empty;

                // Prohibited annotation types
                if (ProhibitedAnnotationTypes.Contains(subtype))
                    v.Add(E("6.5.3", $"Annotation type /{subtype} is not permitted in PDF/A-1.", pageNumber: page));

                // Print flag (bit 2 = 0x4) must be set
                var flags = (int)(dict.Get<PdfInteger>("F")?.Value ?? 0);
                if ((flags & 4) == 0 && subtype != "Widget")
                    v.Add(E("6.5.3", $"/{subtype} annotation on page {page} does not have the Print flag (bit 3) set.", pageNumber: page));

                // Widget annotations must have an appearance stream
                if (subtype == "Widget" && dict[PdfName.AP] is null)
                    v.Add(E("6.5.4", $"Widget annotation on page {page} is missing an appearance stream (/AP).", pageNumber: page));
            }
        }
    }

    private static void CheckActions(PdfDocumentCore core, ICollection<PdfAViolation> v)
    {
        // Catalog must not have /AA (additional actions)
        if (core.Catalog["AA"] is not null)
            v.Add(E("6.6.1", "Catalog contains /AA (additional actions), which is not permitted in PDF/A-1."));

        // Catalog /OpenAction must not be a prohibited action
        CheckActionDict(core.Catalog[PdfName.OpenAction], core, "catalog /OpenAction", v);

        // Scan all objects for prohibited action dicts
        foreach (var obj in core.CollectObjects())
        {
            if (obj.Value is not PdfDictionary dict)
                continue;

            // Check /A entry (action on annotations, links, etc.)
            CheckActionDict(dict[PdfName.A], core, $"object {obj.ObjectNumber}", v, obj.ObjectNumber);

            // Check /AA entry (additional actions on fields/pages)
            if (dict[PdfName.AA] is not PdfDictionary aaDict)
                continue;

            foreach (var (_, aAction) in aaDict.Entries)
                CheckActionDict(aAction, core, $"object {obj.ObjectNumber} /AA", v, obj.ObjectNumber);
        }
    }

    private static void CheckActionDict(
        PdfObject? actionObj,
        PdfDocumentCore core,
        string location,
        ICollection<PdfAViolation> v,
        int? objNum = null
    )
    {
        if (actionObj is null)
            return;

        var dict = core.ResolveDict(actionObj);

        if (dict is null)
            return;

        var actionType = dict.GetName("S") ?? string.Empty;

        if (ProhibitedActionTypes.Contains(actionType))
            v.Add(E("6.6.2", $"Action type /{actionType} in {location} is not permitted in PDF/A-1.", objNum));
    }

    // ── §6.7 Metadata ─────────────────────────────────────────────────────────

    private static void CheckXmpMetadata(PdfDocumentCore core, PdfAProfile profile, ICollection<PdfAViolation> v)
    {
        var metaObj = core.Catalog[PdfName.Metadata];
        var metaStream = metaObj switch
        {
            PdfStream s => s,
            PdfIndirectReference r => core.ResolveIndirect(r.ObjectNumber).Value as PdfStream,
            _ => null
        };

        if (metaStream is null)
        {
            v.Add(E("6.7.2", "Catalog is missing required /Metadata XMP stream."));
            return;
        }

        string xmp;
        try
        {
            xmp = StreamFilters.Decode(metaStream).Span.FromUtf8Span();
        }
        catch
        {
            v.Add(E("6.7.2", "Could not decode /Metadata stream."));
            return;
        }

        // Check pdfaid namespace properties
        try
        {
            var doc = XDocument.Parse(xmp);
            XNamespace pdfaidNs = "http://www.aiim.org/pdfa/ns/id/";

            var part = doc.Descendants(pdfaidNs + "part").FirstOrDefault()?.Value.Trim();
            var conf = doc.Descendants(pdfaidNs + "conformance").FirstOrDefault()?.Value.Trim();

            var expectedPart = profile switch
            {
                PdfAProfile.PdfA1B or PdfAProfile.PdfA1A => "1",
                PdfAProfile.PdfA2B or PdfAProfile.PdfA2U => "2",
                PdfAProfile.PdfA3B => "3",
                _ => "1"
            };
            var expectedConf = profile switch
            {
                PdfAProfile.PdfA1A or PdfAProfile.PdfA2U => "A",
                _ => "B"
            };

            if (part is null)
                v.Add(E("6.7.2", "XMP metadata is missing required pdfaid:part property."));
            else if (part != expectedPart)
                v.Add(E("6.7.2", $"pdfaid:part is '{part}' but expected '{expectedPart}' for {profile}."));

            if (conf is null)
                v.Add(E("6.7.2", "XMP metadata is missing required pdfaid:conformance property."));
            else if (!string.Equals(conf, expectedConf, StringComparison.OrdinalIgnoreCase))
                v.Add(E("6.7.2", $"pdfaid:conformance is '{conf}' but expected '{expectedConf}' for {profile}."));
        }
        catch (Exception ex)
        {
            v.Add(E("6.7.2", $"XMP metadata is not well-formed XML: {ex.Message}"));
        }
    }

    // ── §6.8 Embedded files / Collections ────────────────────────────────────

    private static void CheckEmbeddedFiles(PdfDocumentCore core, ICollection<PdfAViolation> v)
    {
        // /Names /EmbeddedFiles must not be present (for PDF/A-1 and PDF/A-2)
        var names = Resolve<PdfDictionary>(core.Catalog[PdfName.Names], core);
        if (names?[PdfName.EmbeddedFiles] is not null)
            v.Add(E("6.8", "Embedded file attachments (/Names /EmbeddedFiles) are not permitted in PDF/A-1/2."));
    }

    private static void CheckCollection(PdfDocumentCore core, ICollection<PdfAViolation> v)
    {
        if (core.Catalog[PdfName.Collection] is not null)
            v.Add(E("6.8", "/Collection (PDF Portfolio) is not permitted in PDF/A."));
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

    // ReSharper disable once BadListLineBreaks
    private static PdfAViolation E(
        string ruleId,
        string description,
        int? objectNumber = null,
        int? pageNumber = null
    ) =>
        new(ruleId, description, PdfAViolationSeverity.Error, objectNumber, pageNumber);

    // ReSharper disable once BadListLineBreaks
    private static PdfAViolation W(
        string ruleId,
        string description,
        int? objectNumber = null,
        int? pageNumber = null
    ) =>
        new(ruleId, description, PdfAViolationSeverity.Warning, objectNumber, pageNumber);
}
