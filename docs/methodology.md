# Methodology

## Principles

- **Equivalent work.** Every library receives identical inputs (the same Noto font files, image bytes, text, table data,
  business data, HTML and input PDFs) and must produce output with the same content, validated independently.
- **Public APIs, documented usage.** Each adapter uses the library's public API and its documented pattern. Reusable
  objects are created once where the API offers them (UnlimitedPDF `PdfFont`, PDFsharp `XFont` through an `IFontResolver`,
  SkiaSharp `SKTypeface`/`SKFont`/`SKShaper`); where the API has no reusable object the work happens per document
  (PdfPig `AddTrueTypeFont` on each `PdfDocumentBuilder`). No private APIs, reflection or vendor-specific tuning.
- **Nothing hidden in setup.** Fixture generation, adapter construction and validation happen outside measured methods.
  Decoding of input bytes (images, input PDFs, HTML) is inside the measured operation for every library, because turning
  those inputs into PDF content is part of the work being compared.
- **Separate categories.** In-memory generation, file output, cold start, warm steady state, concurrency and long-running
  stress are reported separately and never combined into one score.

## Measurement categories

| Category | How |
|---|---|
| In-memory generation and serialization | BenchmarkDotNet; each method builds the document and serializes it to a `byte[]` (PDF finalisation included). |
| File output | BenchmarkDotNet; the library's own file API writes to a local temporary folder (includes OS file I/O and caching). |
| Cold start | ResourceProfiler; a fresh process per run: process start to main, library initialisation (adapter construction including font loading), first document, process start to first valid PDF, and the parent-observed process lifetime. Runs alternate between libraries. |
| Warm steady state | BenchmarkDotNet's warm-up and measurement iterations; the profiler's sequential suite after one warm-up document per worker. |
| Concurrency | ResourceProfiler; 1, 2, 4, 8 and 16 worker threads, each with its own adapter instance (no shared document or font objects across threads, because thread safety of shared objects is not documented for every library), taking documents from a shared counter. |
| Long-running | ResourceProfiler; 1,000 (full) or 10,000 (stress) documents with resource samples at a fixed interval. |

## Validation

Validation is independent of the producer. For every output:

- **PDFium** (Docnet.Core 2.6.0, never benchmarked) opens the file and checks the page count, the page size (A4 ±1.5 pt),
  the extracted text of each page or of the document (compared with whitespace removed and NFC applied), and renders page
  1 to check that it is not blank and, where images or colour are expected, that it contains saturated colour.
- **Structure**: fonts actually selected by `Tf` operators and images actually painted by `Do` operators (following form
  XObjects) are inspected with PDFsharp's content reader, except for PDFsharp's and MigraDoc's own output, which PdfPig
  inspects. Declared but unused resources are ignored.
- Table scenarios count every row id across the document; manipulation scenarios check the source page marker of every
  output page, which verifies page order; split scenarios validate every part.

Statuses: **Passed**, **Failed validation**, **Unsupported** (not offered by the public API; declared, never emulated),
**Execution failed**, **Timed out** (20 minutes per case by default) and **Not executed**. Only Passed cases are measured.
Byte-for-byte identical PDFs are not required, because metadata, compression and object structure legitimately differ.

Not automated: glyph-level shaping comparison for complex scripts (text extraction must match exactly, and PDFium renders
are kept in `samples/` for visual inspection), PDF/A or other conformance checks, and cross-library visual diffs.

## BenchmarkDotNet

Release build, no debugger, BenchmarkDotNet's default job for standard and full profiles (adaptive warm-up, pilot and
measurement), `ShortRun` for quick, `Dry` for smoke and a Monitoring job (1 warm-up and 5 single invocations) for stress
sizes whose single operation can take seconds to minutes. MemoryDiagnoser reports allocated bytes per operation and
Gen0/1/2 collections per 1,000 operations. Exports: GitHub Markdown, CSV, HTML and full JSON (with all measurements).
Reported statistics: mean, median, min, max, standard deviation, standard error, P95 and the 99.9% confidence interval.

Ratios: **time ratio = competitor mean / UnlimitedPDF mean**; **allocation ratio = competitor allocated bytes per
operation / UnlimitedPDF's**, for the same scenario and parameters. Above 1 means more time or more managed allocation.
They are not calculated when the baseline is unsupported, failed, unmeasured or zero. Absolute values are always shown.

## Memory terminology

| Metric | Source | Meaning |
|---|---|---|
| Allocated per operation | BenchmarkDotNet MemoryDiagnoser | Cumulative managed bytes allocated during one operation. Not live, retained, peak or native memory. |
| Gen0/1/2 per 1,000 ops | MemoryDiagnoser | Collections, not leaks. |
| Managed allocated per document | `GC.GetTotalAllocatedBytes` (profiler) | Same idea across a profiler run, divided by documents. |
| Managed heap in use | `GC.GetTotalMemory(false)` samples | Managed memory currently held, including garbage not yet collected. |
| Retained managed heap | `GC.GetTotalMemory` after two full compacting collections | Live managed memory after the workload. |
| Working set | `Process.WorkingSet64` / `PeakWorkingSet64` | Physical memory of the whole process: managed heap, native allocations, runtime, loaded images. |
| Private bytes | `Process.PrivateMemorySize64` | Committed memory not shareable with other processes. |

These values are never added, subtracted or merged. Native memory is not estimated by subtracting the managed heap from
the working set. Growth over time is reported as an observation; it is not called a leak without profiling evidence.

Sampling: a background thread calls `Process.Refresh` every 100 ms (full) or 250 ms (stress); its own overhead is part
of the measured process. Peak working set comes from the OS counter, other peaks from samples, so short spikes between
samples can be missed. Thread and handle counts are OS values (handles are not available on every platform).

## Library-specific differences

| Topic | Difference and how it is handled |
|---|---|
| Fonts | All libraries draw text with the same Noto Sans files. PDFsharp 6 cannot draw without a font resolver and always embeds; SkiaSharp always embeds the typeface (the 4.157.0 build embeds large font programs, so its outputs are much larger); UnlimitedPDF and PdfPig can also use the non-embedded standard Helvetica, measured separately as the default configuration. UnlimitedPDF pages also declare an unused Helvetica resource; the validator ignores unused resources. |
| Font loading | UnlimitedPDF `PdfFont`, PDFsharp `XFont` and SkiaSharp typefaces are created once per adapter. PdfPig parses the font for each document because `AddTrueTypeFont` belongs to a single builder. |
| Table cell text | UnlimitedPDF 1.1.0 has no table cell font-size setting; MigraDoc tables use 10 pt. Both use the same Noto fonts, bold headers on a light grey background and right-aligned amounts. Page counts can differ because the layout engines differ; validation therefore checks every row rather than a page count. |
| Merge | UnlimitedPDF's `PdfOperations.Merge` adds one bookmark per input file; PDFsharp (page import) and PdfPig (`PdfMerger`) do not. This extra work is part of UnlimitedPDF's documented one-call merge. |
| Remove pages | PDFsharp opens in Modify mode and removes pages; UnlimitedPDF uses `PdfOperations.Remove`; PdfPig copies the remaining pages with `PdfDocumentBuilder.AddPage(document, page)`. |
| Reorder | UnlimitedPDF's `PdfPageRange` requires first ≤ last, so a reversed order is passed as descending single-page ranges (what `PdfPageRange.Parse("9-7")` produces). |
| Images | PDF embeds JPEG data directly and re-encodes PNG pixels, so formats are reported separately. SkiaSharp 4.157.0 crashed natively when a lazily decoded PNG was drawn into a PDF canvas, so its adapter decodes PNGs to a raster image first (inside the measured operation). MigraDoc only accepts in-memory images as `base64:` strings, so the encoding is inside its measured operation. |
| Complex scripts | UnlimitedPDF shapes Devanagari, Telugu and Tamil natively. SkiaSharp uses SkiaSharp.HarfBuzz and writes text runs with UTF-8 clusters so text extracts correctly. PDFsharp and PdfPig do not document complex-script shaping and run English only. |
| Compression and metadata | Each library's defaults for stream compression, object streams and document metadata are kept and were not aligned. Output size is reported as produced and reflects those defaults. |
| HTML to PDF | Only UnlimitedPDF qualifies; browser-based converters are excluded (see library-licenses.md). Its warnings for skipped features are recorded per case. |

## Reproducibility and provenance

Every run records a run id, UTC start time, git commit and dirty flag, profile and arguments, OS, CPU model, logical and
physical cores, total memory, runtime and SDK versions, GC mode, build configuration, debugger state, power plan
(Windows), a hash of the machine name, component versions (including the Skia native version), the fixture version and
a SHA-256 over all fixtures and fonts. BenchmarkDotNet adds its own host information to its exports. Reports are
regenerated from these raw files only.

Run on controlled hardware without a debugger, close other applications, use a high-performance power plan, and do not
compare runs from different machines or runtimes without qualification. GitHub-hosted runner results are informational.
