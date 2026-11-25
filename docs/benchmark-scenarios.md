# Benchmark scenarios

Every scenario is a BenchmarkDotNet class in `src/UnlimitedPDF.Benchmarks.Runner/Benchmarks` with one method per library.
Sizes come from the execution profile (`src/UnlimitedPDF.Benchmarks.Core/Profiles/BenchmarkProfile.cs`). Page geometry is
A4 (595 × 842 pt) with 50 pt margins unless stated. All text uses the Noto fonts in `assets/fonts` unless stated.

## Generation (in memory)

| Scenario (class) | Workload | Sizes (full / stress) | Validation |
|---|---|---|---|
| Empty document (`EmptyDocumentBenchmarks`) | Blank pages | 1, 10 pages | Page count, A4 size |
| Plain text, embedded font (`TextBenchmarks`) | 45 positioned 11 pt lines per page, deterministic words, each line prefixed with a page/line id | 1, 10, 50, 100, 500, 1,000 / 5,000 pages | Page count; every line id and the first full line on each page; every used font embedded |
| Plain text, standard font (`StandardFontTextBenchmarks`) | Same text in the library's built-in Helvetica (default configuration) | 1, 10, 100, 1,000 | As above, but a non-embedded standard font must be used |
| Rich text (`RichTextBenchmarks`) | 18 pt bold heading, six blocks of 12 pt bold, 11 pt regular and 10 pt italic lines | 1, 10, 50, 100 / 500 | Every line on every page; fonts embedded |
| Coloured rich text (`ColoredTextBenchmarks`) | The rich-text layout in five colours | 1, 10, 50, 100 / 500 | As rich text, plus rendered colour |
| Images (`ImageBenchmarks`) | N distinct 320 × 240 px images (PNG or JPEG), four per page at 240 × 180 pt, with captions | 1, 10, 100 images × PNG, JPEG / 500 JPEG | Page count, captions, ≥ N distinct images painted, rendered colour |
| Vector graphics (`VectorBenchmarks`) | 21 lines, 30 stroked and 20 filled rectangles and 20 circles per page | 1, 10, 100 / 1,000 | Page count, page label, rendered colour |
| Mixed content (`MixedContentBenchmarks`) | 20 text lines, one distinct JPEG and 10 filled rectangles per page | 1, 10, 50, 100 / 1,000 | Every line, one image per page, rendered colour, fonts embedded |

## Layout

| Scenario | Workload | Sizes | Validation |
|---|---|---|---|
| Simple table (`TableBenchmarks`) | 4 columns (id, name, department, amount), bold header on grey, repeated on every page, right-aligned amounts | 10, 100, 1,000 / 10,000, 100,000 rows | Header on every page; exactly one occurrence of every row id across the document; first and last rows' cells |
| Table with images (`TableWithImagesBenchmarks`) | 3 columns; a distinct 64 × 48 px PNG above the first cell's text in each row | 10, 100 / 1,000 rows | As above plus ≥ one distinct image per row |
| Headers, footers and page numbers (`HeaderFooterBenchmarks`) | 30 flowing 11 pt lines per page, header text, "Page i of N" footer, explicit break per page | 10, 100, 1,000 / 5,000 pages | Exact page count; header, correct "Page i of N" and every line id on each page |

## Business documents

`BusinessDocumentBenchmarks` renders nine synthetic documents from one library-neutral model (title, label/value fields,
party lines, captioned tables, notes) with automatic pagination: **Invoice** (8 items + totals), **PurchaseOrder** (12),
**Quotation** (6), **SalesReport** (50-row table + regional summary), **BankStatement** (300 transactions, multi-page),
**Payslip** (earnings and deductions), **ProductCatalog** (40 rows with JPEG thumbnails), **FinancialReport** (three
statements) and **TabularReport** (2,000-row table). `InvoiceBatchBenchmarks` generates 100 (full) or 1,000 (stress)
distinct invoices as separate PDFs. Validation: every title, field label and value, party line, caption, header, cell and
note must appear; catalogue images must be painted; fonts embedded. Data is synthetic; no real customer data is used.

## Multilingual

`MultilingualBenchmarks`: 30 lines per page at 14 pt in **English**, **Hindi**, **Telugu**, **Tamil** or **Mixed**
(two scripts per line, with a Latin line number), 1, 10 and 100 pages. Hindi, Telugu and Tamil use the matching Noto
Sans fonts; the sentences exercise conjuncts, vowel signs and reph. Validation requires the exact text of every line to be
extracted (so missing glyphs, `?` substitutions and broken copy-and-paste fail) and every used font embedded. Rendered
samples are kept for visual shaping checks.

## Manipulation

Inputs are written by `FixturePdfWriter` (PDF 1.7, Flate streams, standard Helvetica text and DeviceRGB images), which
no benchmarked library produced, so all libraries parse byte-identical input. Each page carries a marker
"Fixture Dnnn Pnnnn" used to verify page order.

| Scenario | Workload | Sizes | Validation |
|---|---|---|---|
| Merge (`MergeBenchmarks`) | N five-page inputs of text, image or mixed content | 2, 10, 50, 100 × 3 contents / 500 mixed | Page count; every page's marker in order; images present |
| Split per page (`SplitBenchmarks`) | One mixed PDF into single-page PDFs | 10, 100, 1,000 / 5,000 pages | Part count; each part is one page with the right marker |
| Split into ranges (`SplitRangesBenchmarks`) | One mixed PDF into consecutive 10-page parts | 10, 100, 1,000 / 5,000 pages | Part count; each part's pages and order |
| Extract (`ExtractPagesBenchmarks`) | Small: pages 2, 5–7, 9 of 10. Large: 1–50, 500–549, 951–1,000 of 1,000 | Small, Large | Exact pages in order |
| Remove (`RemovePagesBenchmarks`) | The same selections removed | Small, Large | Remaining pages in original order |
| Reorder (`ReorderPagesBenchmarks`) | All pages in reverse order | Small, Large | Reversed order |

## HTML to PDF

`HtmlToPdfBenchmarks`: self-contained HTML (no network; images as `data:` URIs) converted to A4 with 36 pt margins and
Noto Sans registered through the library's font API. **Simple**: headings and paragraphs (5, 100, 1,000 paragraphs).
**TablesCssImages**: a coloured banner, a bordered note, 2, 10 or 50 PNG images and a 20-, 200- or 2,000-row table with
`thead` (repeated header), borders and right-aligned amounts. Advanced layout CSS (flexbox, grid, floats, positioning) is
deliberately excluded because it is not a common feature across converters. Only UnlimitedPDF participates; its warnings
are recorded. Validation: required text, images and embedded fonts.

## File output

`FileOutputBenchmarks`: the plain-text document (10, 100 / 1,000 pages) written through each library's file API to a
local temporary folder; the file is read back and validated.

## Resource profiler

| Suite | Workloads and libraries | Sizes (full / stress) | Metrics |
|---|---|---|---|
| Cold start | text-1-page (UnlimitedPDF, PDFsharp, PdfPig, SkiaSharp), invoice (UnlimitedPDF, MigraDoc), html-simple-small (UnlimitedPDF) | 10 fresh processes per library | Process start → main, library initialisation, first document, process start → first valid PDF, parent-observed process time, working set |
| Sequential | same | 100, 1,000, 10,000 documents | Docs/s, pages/s, per-document P50/P95/P99/max, CPU %, allocation/doc, GC counts, peak working set, private bytes, managed heap, retained heap, threads, handles |
| Concurrent | same | 1, 2, 4, 8, 16 workers × 2,000 documents / 16, 32 workers × 10,000 | As sequential, plus throughput by worker count; failures counted |
| Long-running | text-1-page, invoice | 1,000 / 10,000 documents | Resource time series (working set, private bytes, managed heap, GC heap size, threads, handles) and retained memory |

## Status of planned items

Implemented: every scenario family in the plan except the separations noted here. Not implemented: rendered
pixel-difference comparisons between libraries (renders are kept for inspection instead), a browser-based HTML competitor
(excluded by policy), and separate "comparable configuration" variants for compression settings (library defaults are
used and documented).
