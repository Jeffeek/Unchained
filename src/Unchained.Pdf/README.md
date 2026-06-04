# Unchained.Pdf

MIT-licensed PDF engine for .NET. Load, save, extract text, generate tables, merge documents, add annotations, fill forms, sign, encrypt, validate PDF/A, and import from Markdown, plain text, or SVG — all in pure managed code, no native dependencies.

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
