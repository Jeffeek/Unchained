# Unchained Studio

A Blazor Server inspector for documents processed by the Unchained suite. Upload a
file, browse its internal structure as a tree, inspect any node's properties, and
preview rendered output — all in the browser.

## Running

```bash
cd tools/Unchained.Studio
dotnet run
```

Then open the URL printed in the console (e.g. `http://localhost:5xxx`).

The top app-bar chips switch between format tabs:

| Tab | Route | Status |
|---|---|---|
| PDF | `/` | Full inspector + rendering + operations |
| PPTX | `/pptx` | Inspector + slide preview |
| DOCX | — | Planned |
| XLSX | — | Planned |

## PPTX tab

Upload a `.pptx` presentation (drag-and-drop or Browse). The tab then shows three panels:

- **Left — document tree.** Document → Properties, Slides (each slide expands to its
  shapes, groups recurse), Masters (each with its Theme and Layouts), and Media
  (embedded images).
- **Centre — slide preview.** The selected slide is rasterized with
  `Unchained.Pptx.Rendering` (FreeType2 + HarfBuzz). Navigate with the arrows or the
  slide-number box; zoom 25–200%.
- **Right — properties.** Context-sensitive details for the selected tree node: slide
  metadata and text, shape geometry/type-specific fields, chart data, master/layout/theme
  info, embedded-image details.

Operations bar: **Extract text** (all slides), **Export slide PNG** (current slide at
1920×1080), **Download** (the original bytes), **Close**.

### Slide rendering prerequisite

The slide preview and "Export slide PNG" need the FreeType2 native library. If it's
missing the preview shows a notice instead of an image. Fetch it once:

```bash
bash scripts/FetchNatives/fetch-natives.sh        # auto-detects host
# or: bash scripts/FetchNatives/fetch-natives.sh --rid win-x64
```

### Legacy `.ppt`

Only OOXML `.pptx` is supported. The file picker accepts `.ppt` so the app can show a
clear message — legacy binary `.ppt` (BIFF/OLE compound document) is a different format
and must be converted to `.pptx` first.

## Architecture

The PPTX tab mirrors the PDF tab's structure:

| Concern | PDF | PPTX |
|---|---|---|
| Per-circuit session | `PdfSessionState` | `PptxSessionState` |
| Tree construction | `PdfTreeBuilder` | `PptxTreeBuilder` |
| Property projection | `PdfPropertyAdapter` | `PptxPropertyAdapter` |
| Tab component | `Components/Pdf/PdfTab.razor` | `Components/Pptx/PptxTab.razor` |
| Preview | `Components/Shared/PreviewPanel.razor` | `Components/Pptx/SlidePreviewPanel.razor` |

Shared components (`DocumentTree`, `PropertiesPanel`, `FileDropZone`) and models
(`TreeNode`, `PropertyBag`) are reused as-is. `SessionStateService` holds both the PDF
and PPTX sessions for the circuit; processors are registered as singletons in
`Program.cs` (`DocumentProcessor`, `PresentationProcessor`).
