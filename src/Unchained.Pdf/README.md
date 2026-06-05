# Unchained.Pdf

MIT-licensed PDF engine for .NET. Load, save, extract text, generate tables, merge documents, add annotations, fill forms, sign, encrypt, validate PDF/A, validate PDF/UA, produce linearized (web-optimized) output, generate tagged accessible PDFs, and import from Markdown, plain text, or SVG — all in pure managed code, no native dependencies.

**Targets:** `net8.0` · `net9.0` · `net10.0`  
**License:** MIT

---

## Installation

```xml
<PackageReference Include="Unchained.Pdf" Version="0.1.0" />
```

> For page rendering to PNG, install `Unchained.Pdf.Rendering` instead — it depends on this package.

---

## Usage

### Load & save

```csharp
using Unchained.Pdf.Engine;

var processor = new DocumentProcessor();

// From file or stream
await using var doc = await processor.LoadAsync("invoice.pdf");
await using var fromStream = await processor.LoadAsync(myStream);

// Password-protected
await using var secured = await processor.LoadAsync("secure.pdf", password: "1234");

// Save
await processor.SaveAsync(doc, "output.pdf");
await processor.SaveAsync(doc, outputStream);
```

### Text extraction

```csharp
// Plain text
string text = doc.Pages[1].ExtractText();

// Positioned spans with font name, size, and coordinates
IReadOnlyList<TextSpan> spans = doc.Pages[1].GetTextSpans();
```

### Content operators

```csharp
// Raw PDF content operators for advanced use
IReadOnlyList<ContentOperator> ops = doc.Pages[1].GetContentOperators();
```

### Table generation

```csharp
var generator = new TableGenerator();
var data = new TableData(
    Headers: ["Name", "Amount"],
    Rows:    [["Alice", "$1,200"], ["Bob", "$950"]]
);
await using var tableDoc = await generator.GenerateAsync(data, TableStyle.Default);
await processor.SaveAsync(tableDoc, "report.pdf");
```

### Document merging

```csharp
var merger = new DocumentMerger();
await using var merged = await merger.MergeAsync([doc1, doc2, doc3]);
```

### Stamps & watermarks

```csharp
var applier = new StampApplier();
await applier.StampAsync(doc,
    new TextStamp("DRAFT", position: StampPosition.Center, opacity: 0.25f, rotation: 45));
```

### Annotations

```csharp
var editor = new AnnotationEditor();
editor.AddTextAnnotation(doc.Pages[1], x: 72, y: 720, text: "Please review");

// Export/import XFDF
var xfdfEditor = new XfdfEditor();
string xfdf = xfdfEditor.ExportAnnotationsToXfdf(doc);
await xfdfEditor.ImportAnnotationsFromXfdfAsync(doc, xfdfXml);
```

### Bookmarks

```csharp
var bookmarkEditor = new BookmarkEditor();
bookmarkEditor.AddBookmark(doc, new Bookmark("Introduction", pageNumber: 1));
bookmarkEditor.AddBookmark(doc, new Bookmark("Chapter 1",    pageNumber: 5));

IReadOnlyList<Bookmark> bookmarks = doc.GetBookmarks();
```

### Form filling

```csharp
var filler = new FormFiller();
IReadOnlyList<FormField> fields = doc.GetFormFields();

await filler.FillAsync(doc, new Dictionary<string, string>
{
    ["FirstName"] = "Alice",
    ["LastName"]  = "Smith"
});

await filler.FlattenAsync(doc); // embed field values into page content
```

### Digital signatures

```csharp
using var cert = X509CertificateLoader.LoadPkcs12File("cert.pfx", "pass");

// Sign
await processor.SignAsync(doc, cert, "signed.pdf",
    new SignatureOptions(Reason: "Approved", Location: "Berlin"));

// Verify
var signatures = await processor.VerifySignaturesAsync(pdfBytes);
foreach (var sig in signatures)
    Console.WriteLine($"{sig.SignerName}: valid={sig.IsSignatureValid}");
```

### Encryption

```csharp
// Encrypt
await processor.SaveAsync(doc, "secure.pdf",
    new SaveOptions(Encryption: new EncryptionOptions(
        UserPassword:  "user",
        OwnerPassword: "owner",
        Algorithm:     PdfEncryptionAlgorithm.Aes256)));

// Decrypt on load — transparent
await using var open = await processor.LoadAsync("secure.pdf", password: "user");

// Change passwords
using var ms = new MemoryStream();
await processor.ChangePasswordsAsync(doc, "newUser", "newOwner", ms);
```

### PDF/A validation & conversion

```csharp
// Validate
var result = await processor.ValidatePdfAAsync(pdfBytes, PdfAProfile.PdfA1b);
Console.WriteLine(result.IsConformant);
foreach (var violation in result.Errors)
    Console.WriteLine($"[{violation.RuleId}] {violation.Description}");

// Convert
using var output = new MemoryStream();
await processor.ConvertToPdfAAsync(doc, output, PdfAProfile.PdfA1b);
```

### PDF/UA validation

Validates against ISO 14289-1 (PDF/UA-1). Checks tagged-PDF marker, document language,
structure tree root, parent tree, figure alt text, heading level sequence, table and list
structure, untagged content, annotation accessible names, action restrictions, and XMP
`pdfuaid` metadata.

```csharp
var result = await processor.ValidatePdfUAAsync(pdfBytes);
Console.WriteLine(result.IsConformant);
foreach (var v in result.Violations)
    Console.WriteLine($"[{v.RuleId}] {v.Severity}: {v.Description}");
```

### Linearization (web-optimized output)

Produces a linearized PDF (ISO 32000-1 Annex F) so that PDF readers can render the first
page before the full file downloads. Includes a hint stream, two xref sections, and the
linearization parameter dictionary in the first 1024 bytes.

```csharp
// Via SaveOptions
await processor.SaveAsync(doc, "fast.pdf", new SaveOptions(Linearize: true));

// Via preset
await processor.SaveAsync(doc, "fast.pdf", SaveOptions.WebOptimized);
```

### Tagged PDF (accessibility)

Produces tagged PDFs with a full ISO 32000-1 §14.7 structure tree when converting from
text, Markdown, or SVG. Tagged output passes `ValidatePdfUAAsync` §7.2, §7.4, and §7.5
checks. Set `Tagged: true` on the load options and supply a BCP 47 language tag.

```csharp
// Plain text → tagged PDF
await using var txt = await processor.LoadFromTxtAsync(
    "Accessible content",
    new TxtLoadOptions(Tagged: true, Language: "en-US"));

// Markdown → tagged PDF (H1–H6, P, Code, L/LI/LBody)
await using var md = await processor.LoadFromMarkdownAsync(
    "# Title\n\nParagraph text.\n\n- Item 1\n- Item 2",
    new MdLoadOptions(Tagged: true, Language: "en-US"));

// SVG → tagged PDF (/Figure with /Alt text)
await using var svg = await processor.LoadFromSvgAsync(
    svgString,
    new SvgLoadOptions(Tagged: true, Language: "en-US", AltText: "Chart showing sales data"));

// Validate the result
using var ms = new MemoryStream();
await processor.SaveAsync(md, ms);
var uaResult = await processor.ValidatePdfUAAsync(ms.ToArray());
```

### Format imports

```csharp
// Plain text → PDF (word-wrap, pagination, configurable font/margins)
await using var txt = await processor.LoadFromTxtAsync(
    "Long text content...",
    new TxtLoadOptions(FontSize: 12, PageMargin: 72));

// Markdown → PDF (headings, bold/italic, lists, code, thematic breaks)
await using var md = await processor.LoadFromMarkdownAsync(
    "# Title\n\n- Item 1\n- Item 2\n\n**Bold text**");

// SVG → PDF (shapes, paths, text, transforms, viewBox)
await using var svg = await processor.LoadFromSvgAsync(svgString);
```

### Viewer preferences & metadata

```csharp
// Viewer preferences
var prefEditor = new ViewerPreferencesEditor();
prefEditor.SetHideToolbar(doc, true);
prefEditor.SetPageLayout(doc, PageLayout.TwoColumnLeft);

// XMP metadata
var xmpEditor = new XmpMetadataEditor();
xmpEditor.SetXmpMetadata(doc, "<x:xmpmeta>...</x:xmpmeta>");
string? xmp = doc.GetXmpMetadata();

// Named destinations
var destEditor = new NamedDestinationEditor();
destEditor.AddDestination(doc, new NamedDestination("toc", pageNumber: 3));
```

### Optimization & repair

```csharp
var optimizer = new DocumentOptimizer();
await optimizer.OptimizeAsync(doc);          // compress uncompressed streams
await optimizer.OptimizeResourcesAsync(doc); // deduplicate identical objects

// Recover from corrupted files
await using var repaired = await processor.RepairAsync(corruptedBytes);
```

---

## All stream filters — pure managed

FlateDecode · LZWDecode · CCITTFaxDecode · ASCIIHexDecode · ASCII85Decode · RunLengthDecode · DCTDecode (JPEG) · JBIG2Decode · JPXDecode (JPEG 2000)

---

## License

MIT. No AGPL/GPL dependencies. Safe for commercial and open-source use.

[GitHub](https://github.com/Jeffeek/Unchained) · [Report an issue](https://github.com/Jeffeek/Unchained/issues)
