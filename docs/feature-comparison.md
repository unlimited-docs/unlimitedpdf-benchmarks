# Capability matrix

Classification per library version, from each package's public API surface (inspected by reflection on 2026-11-24),
its official documentation, and execution in this suite. Marketing descriptions were not used as evidence.

Legend: **Native** = supported by the library's public API · **Companion** = through a documented companion package ·
**Custom** = would need a substantial custom implementation (not benchmarked, because it is not comparable with a native
high-level feature) · **No** = not offered by the public API · **Unverified** = not established either way.
✔ marks capabilities exercised and independently validated by this suite.

| Capability | UnlimitedPDF 1.1.0 | PDFsharp 6.2.4 | PDFsharp-MigraDoc 6.2.4 | PdfPig 0.1.16 | SkiaSharp 4.157.0 |
|---|---|---|---|---|---|
| Empty PDF creation | Native ✔ | Native ✔ | Native | Native ✔ | Native ✔ |
| Text rendering (positioned) | Native ✔ (`PdfPage.AddText`) | Native ✔ (`XGraphics.DrawString`) | Flowing paragraphs ✔ | Native ✔ (`AddText`) | Native ✔ (`SKCanvas.DrawText`) |
| Text formatting: sizes, bold/italic faces | Native ✔ (one font file per face) | Native ✔ | Native ✔ | Native ✔ | Native ✔ |
| Text colour | No (no colour parameter on text APIs) | Native ✔ | Native | Native ✔ | Native ✔ |
| Custom TrueType/OpenType fonts | Native ✔ (`PdfFont`) | Native ✔ (`IFontResolver`) | Native ✔ (via PDFsharp) | Native ✔ (`AddTrueTypeFont`) | Native ✔ (`SKTypeface`) |
| Standard (non-embedded) fonts | Native ✔ (Helvetica) | No (always embeds) | No | Native ✔ (Standard 14) | No (always embeds) |
| Font embedding | Native ✔ (subset of used glyphs) | Native ✔ | Native ✔ | Native ✔ | Native ✔ |
| Unicode and complex scripts (Devanagari, Telugu, Tamil) | Native ✔ (shaping, fallback fonts) | Unverified; no shaping documented (not benchmarked) | Unverified (not benchmarked) | No shaping documented (not benchmarked) | Companion ✔ (SkiaSharp.HarfBuzz) |
| Images (JPEG, PNG) | Native ✔ | Native ✔ | Native ✔ (in table cells) | Native ✔ | Native ✔ |
| Vector graphics | Filled rectangles only ✔; no lines, strokes or curves | Native ✔ | Shapes via PDFsharp | Native ✔ (lines, rectangles, circles) | Native ✔ |
| Tables | Native ✔ | Custom (Companion: MigraDoc) | Native ✔ | Custom | Custom |
| Automatic pagination | Native ✔ (tables, body flow) | Custom (Companion: MigraDoc) | Native ✔ | Custom | Custom |
| Repeating table headers | Native ✔ | Companion | Native ✔ | Custom | Custom |
| Headers and footers | Native ✔ | Custom (Companion: MigraDoc) | Native ✔ | Custom | Custom |
| Page numbers ("Page i of N") | Native ✔ (`{page}`, `{pages}`) | Custom (Companion: MigraDoc) | Native ✔ (page fields) | Custom | Custom |
| PDF merging | Native ✔ (`PdfOperations.Merge`) | Native ✔ (page import) | No | Native ✔ (`PdfMerger`) | No |
| PDF splitting | Native ✔ (`PdfOperations.Split`) | Native ✔ (page import) | No | Native ✔ (builder page copy) | No |
| Page extraction / reordering | Native ✔ (`PdfOperations.Extract`) | Native ✔ | No | Native ✔ (builder page copy) | No |
| Page removal | Native ✔ (`PdfOperations.Remove`) | Native ✔ (`Pages.RemoveAt`) | No | Native ✔ (copy of remaining pages) | No |
| Text extraction | No public API in 1.1.0 | No (content parsing only) | No | Native | No |
| HTML to PDF | Native ✔ (built-in renderer) | No | No | No | No |
| Stream / in-memory output | Native ✔ | Native ✔ | Native ✔ | Native ✔ | Native ✔ |
| File output | Native ✔ | Native ✔ | Native | Native ✔ | Native ✔ |
| Cross-platform (Windows, Linux) | Pure managed (net6–net11) | Managed; fonts need a resolver | Managed | Managed (netstandard) | Native binaries per platform (win, linux, macOS provided) |
| Native dependencies | None | None | None | None | libSkiaSharp, libHarfBuzzSharp |
| Independent documents in parallel | ✔ (one adapter per thread) | ✔ | ✔ | ✔ | ✔ |

Notes:

- "Independent documents in parallel" means separate document and font objects per thread, as measured by the profiler.
  Sharing one document or font object across threads is not claimed for any library.
- PDFsharp has no complex-script shaping API in its documentation, so Hindi, Telugu, Tamil and mixed-script cases are
  marked Unsupported for it rather than measured with incorrect output. The same applies to PdfPig.
- Browser-based HTML conversion (Microsoft.Playwright with Chromium) is excluded from the suite; see
  [library-licenses.md](library-licenses.md#excluded-candidates).

## Scenario participation

| Scenario | UnlimitedPDF | PDFsharp | MigraDoc | PdfPig | SkiaSharp |
|---|---|---|---|---|---|
| Empty document | Baseline | ✔ | N/A | ✔ | ✔ |
| Plain text (embedded font) | Baseline | ✔ | N/A | ✔ | ✔ |
| Plain text (standard font) | Baseline | N/A | N/A | ✔ | N/A |
| Rich text (sizes, faces) | Baseline | ✔ | N/A | ✔ | ✔ |
| Coloured rich text | N/A (no baseline) | ✔ | N/A | ✔ | ✔ |
| Images | Baseline | ✔ | N/A | ✔ | ✔ |
| Vector graphics | N/A (no baseline) | ✔ | N/A | ✔ | ✔ |
| Mixed content | Baseline | ✔ | N/A | ✔ | ✔ |
| Tables, tables with images, headers/footers | Baseline | N/A | ✔ | N/A | N/A |
| Business documents, invoice batches | Baseline | N/A | ✔ | N/A | N/A |
| Multilingual | Baseline | English only | N/A | English only | ✔ |
| Merge, split, extract, remove, reorder | Baseline | ✔ | N/A | ✔ | N/A |
| HTML to PDF | Only participant | N/A | N/A | N/A | N/A |
| File output | Baseline | ✔ | N/A | ✔ | ✔ |
| Profiler: text workload | Baseline | ✔ | — | ✔ | ✔ |
| Profiler: invoice workload | Baseline | — | ✔ | — | — |
| Profiler: HTML workload | Only participant | — | — | — | — |
