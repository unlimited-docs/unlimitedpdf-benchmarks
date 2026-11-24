# UnlimitedPDF.Benchmarks

Reproducible .NET benchmarks comparing the published [UnlimitedPDF](https://www.nuget.org/packages/UnlimitedPDF/) NuGet
package with commercially free, open-source PDF libraries: execution time, managed allocation, garbage collection,
process memory, throughput, latency, cold start, scalability, output size and output correctness.

This is a neutral benchmarking project, not a marketing demonstration. UnlimitedPDF is the **baseline** that ratios are
calculated against, not a presumed winner, and results are published whichever library performs better.

> **Affiliation.** This repository is associated with UnlimitedPDF. It benchmarks only the public NuGet release, never a
> modified build. Other library names and trademarks belong to their respective owners. Maintainers of the other libraries
> and independent contributors are invited to review and improve every implementation here.

## Contents

- [Objectives](#objectives) · [Libraries](#libraries) · [Licensing criteria](#commercial-licensing-criteria)
- [Feature matrix](#feature-matrix) · [Benchmark categories](#benchmark-categories) · [Architecture](#architecture)
- [Getting started](#getting-started) · [Running benchmarks](#running-benchmarks) · [Profiles](#execution-profiles)
- [Results and reports](#results-and-reports) · [Methodology](#methodology-summary) · [Limitations](#known-limitations)
- [Contributing](#contributing) · [License](#license)

## Objectives

1. **Performance**: execution time, per-document latency and throughput.
2. **Memory efficiency**: managed allocation and GC, and separately process working set, private bytes and retained heap.
3. **Output correctness**: every output is validated independently before it is measured; invalid output is never ranked.
4. **Commercial freedom**: only libraries usable in proprietary commercial software without fees, revenue thresholds,
   watermarks or source-disclosure obligations.

## Libraries

| Library | Package and version | Licence | Role |
|---|---|---|---|
| UnlimitedPDF | `UnlimitedPDF` 1.1.0 | Apache-2.0 | Baseline |
| PDFsharp | `PDFsharp` 6.2.4 | MIT | Drawing and page manipulation (last MIT release; 6.5.0+ is AGPL) |
| PDFsharp-MigraDoc | `PDFsharp-MigraDoc` 6.2.4 | MIT | PDFsharp's official layout companion: tables, headers, flowing documents |
| PdfPig | `PdfPig` 0.1.16 | Apache-2.0 | Document builder and merger |
| SkiaSharp | `SkiaSharp` 4.157.0 (+ `SkiaSharp.HarfBuzz`) | MIT | Skia PDF backend; HarfBuzz shaping for Indic scripts |

Excluded after review (details in [library licenses](docs/library-licenses.md)): **Microsoft.Playwright**, because HTML
to PDF through it requires a separately downloaded Chromium browser and a Node.js driver running as external processes,
and this suite compares self-contained NuGet libraries only. Also excluded: PDFsharp/MigraDoc 6.5.0+ (AGPL-3.0-only),
current QuestPDF (revenue-conditioned community licence) and iText (AGPL/commercial). Validation uses PDFium through
`Docnet.Core` 2.6.0 (MIT); it is never benchmarked.

## Commercial licensing criteria

A library in the primary suite must be open source; allow use in proprietary commercial applications without buying a
licence; have no revenue threshold, no developer, server, deployment or document-count limits and no watermark; and not
require disclosure of application source code merely because it is used. Permissive licences (MIT, Apache-2.0, BSD)
are preferred. Licences are verified per exact package version, including transitive packages and native binaries, and
`scripts/check-licenses.ps1` enforces the policy in CI. Copyleft licences are not "non-commercial"; their obligations are
simply outside this project's permissive policy.

## Feature matrix

Summary of [docs/feature-comparison.md](docs/feature-comparison.md) (N = native, C = companion package, — = not offered
by the public API, so not benchmarked):

| Capability | UnlimitedPDF | PDFsharp | MigraDoc | PdfPig | SkiaSharp |
|---|---|---|---|---|---|
| Positioned text, embedded TrueType fonts | N | N | — | N | N |
| Standard (non-embedded) Helvetica | N | — | — | N | — |
| Bold/italic faces, sizes | N | N | N | N | N |
| Text colour | — | N | N | N | N |
| Images (JPEG, PNG) | N | N | N | N | N |
| Lines, strokes, curves | — (filled rectangles only) | N | — | N | N |
| Tables with pagination and repeated headers | N | — (C: MigraDoc) | N | — | — |
| Headers, footers, page numbers, flowing body | N | — (C: MigraDoc) | N | — | — |
| Devanagari, Telugu, Tamil shaping | N | — | — | — | C (HarfBuzz) |
| Merge, split, extract, remove, reorder pages | N | N | — | N (merge native, rest via builder copy) | — |
| HTML to PDF | N | — | — | — | — |

## Benchmark categories

Measurement categories are reported separately and never combined into a single score.

| Category | Tool | What it covers |
|---|---|---|
| In-memory generation | BenchmarkDotNet | Empty, plain text (embedded and standard font), rich and coloured text, images, vector graphics, mixed content |
| Layout | BenchmarkDotNet | Tables (10 to 100,000 rows), tables with images, headers/footers/page numbers |
| Business documents | BenchmarkDotNet | Invoice, purchase order, quotation, sales report, bank statement, payslip, catalogue, financial and tabular reports; invoice batches |
| Multilingual | BenchmarkDotNet | English, Hindi, Telugu, Tamil and mixed scripts |
| Manipulation | BenchmarkDotNet | Merge, split per page and per range, extract, remove and reorder pages |
| HTML to PDF | BenchmarkDotNet | Simple HTML and HTML with tables, CSS and images (UnlimitedPDF only; absolute values) |
| File output | BenchmarkDotNet | End-to-end generation written to a file |
| Cold start | ResourceProfiler | Fresh process: process start to first valid PDF, library initialisation |
| Sequential and concurrent | ResourceProfiler | Docs/s, pages/s, P50/P95/P99 latency, CPU, 1 to 16 workers |
| Long-running and stress | ResourceProfiler | Working set, private bytes, managed heap, retained heap, threads, handles over 1,000 to 10,000 documents |

Every scenario, its sizes and its validation rules are listed in [docs/benchmark-scenarios.md](docs/benchmark-scenarios.md).

## Architecture

```
src/
  UnlimitedPDF.Benchmarks.Core/              fixtures, independent validation, profiles, provenance (no benchmarked code paths)
  UnlimitedPDF.Benchmarks.Adapters/          one folder per library; capability interfaces (ITableGenerator, IPdfMerger, ...)
  UnlimitedPDF.Benchmarks.Runner/            BenchmarkDotNet scenarios, validation pass, validated-case filter
  UnlimitedPDF.Benchmarks.ResourceProfiler/  isolated child processes: cold start, throughput, latency, memory over time
  UnlimitedPDF.Benchmarks.Reporting/         report.html, summary.md, results.csv, results.json from raw data
tests/
  UnlimitedPDF.Benchmarks.UnitTests/         fixtures, statistics, validator rejection cases, profiles
  UnlimitedPDF.Benchmarks.IntegrationTests/  every scenario x library validated by PDFium; profiler; reporting
assets/fonts/                                Noto fonts (SIL OFL 1.1) shared by every library
scripts/                                     run-benchmarks, generate-reports, check-licenses
```

Details: [docs/architecture.md](docs/architecture.md).

## Getting started

Prerequisites: the .NET 10 SDK on Windows or Linux (x64). No paid services, licence keys, cloud resources, browsers or
network access are needed after NuGet restore.

```sh
dotnet restore UnlimitedPDF.Benchmarks.slnx --locked-mode
dotnet build UnlimitedPDF.Benchmarks.slnx -c Release --no-restore
dotnet test UnlimitedPDF.Benchmarks.slnx -c Release --no-build --no-restore
pwsh ./scripts/check-licenses.ps1
```

## Running benchmarks

The scripts build, validate every case, measure the passing cases with BenchmarkDotNet, run the resource profiler in
isolated processes and generate the reports into `results/<run-id>/`.

```powershell
./scripts/run-benchmarks.ps1 -Profile quick
./scripts/run-benchmarks.ps1 -Profile standard -Category Manipulation -Library UnlimitedPdf,PdfPig
./scripts/run-benchmarks.ps1 -Profile all            # full, then stress, each in its own run folder
./scripts/run-benchmarks.ps1 -Profile smoke -- --filter '*TableBenchmarks*'
```

```sh
bash scripts/run-benchmarks.sh --profile quick
bash scripts/run-benchmarks.sh --profile standard --category Layout --library UnlimitedPdf,MigraDoc
```

Direct `dotnet` commands (the scripts run exactly these):

```sh
dotnet run --project src/UnlimitedPDF.Benchmarks.Runner -c Release --no-build -- validate --profile standard --output results/my-run
dotnet run --project src/UnlimitedPDF.Benchmarks.Runner -c Release --no-build -- bench --profile standard --output results/my-run --filter '*Merge*'
dotnet run --project src/UnlimitedPDF.Benchmarks.ResourceProfiler -c Release --no-build -- --profile standard --output results/my-run
dotnet run --project src/UnlimitedPDF.Benchmarks.Reporting -c Release --no-build -- results/my-run
dotnet run --project src/UnlimitedPDF.Benchmarks.Runner -c Release --no-build -- list --profile full
```

Filtering: `--library` (UnlimitedPdf, PdfSharp, MigraDoc, PdfPig, SkiaSharp), `--category` (comma-separated: Generation,
Layout, BusinessDocuments, Multilingual, Manipulation, HtmlToPdf, FileOutput), any BenchmarkDotNet option such as
`--filter '*TextBenchmarks.PdfPig*'`, `--job short` or `--runtimes net10.0`, and for the profiler `--suite ColdStart,Concurrent`.
Document sizes and worker counts come from the profile.

## Execution profiles

| Profile | BenchmarkDotNet job | Purpose |
|---|---|---|
| `smoke` | Dry (one invocation) | Proves every implementation compiles, runs and produces valid PDFs; not a measurement |
| `quick` | ShortRun | Reduced sizes for local development |
| `standard` | Default | Representative comparison |
| `full` | Default | Every standard size (text 1 to 1,000 pages, tables to 1,000 rows, merges to 100 files, 1 to 16 workers) |
| `stress` | Monitoring (1 warm-up, 5 single invocations) | Largest sizes only: 5,000-page text, 100,000-row tables, 500-file merges, 10,000-document long runs |

`full` and `stress` together cover every planned size once.

## Results and reports

Each run folder contains `manifest.json` (run id, UTC time, commit, environment, component versions, fixture checksum),
`validation.json` (status of every case), `samples/` (representative PDFs and PDFium renders), `bdn/` (raw
BenchmarkDotNet logs and Markdown, CSV, HTML and JSON exports), `profiler/results.json`, and the generated
`report.html` (self-contained, with charts), `summary.md`, `results.csv` and `results.json`. Reports can be regenerated
from the raw files with `scripts/generate-reports.ps1`. Raw run folders are not committed; documented runs are in
[docs/results](docs/results/).

## Methodology summary

Fixtures are deterministic and identical for every library, including the same font files and image bytes; manipulation
inputs come from an independent PDF writer that no benchmarked library produced. Preparation and validation happen outside
measured methods. Each output is validated by PDFium (page count, size, extracted text, rendered ink and colour) and by a
parser other than its producer (font embedding and images). Only passed cases are measured, and ratios are calculated
only against a passed baseline. See [methodology](docs/methodology.md) and
[interpreting results](docs/interpreting-results.md).

## Known limitations

- One machine's results do not transfer to another; GitHub-hosted runners are informational only.
- Library defaults differ (compression, metadata, font subsetting); output sizes and some costs reflect those defaults.
- UnlimitedPDF 1.1.0 has no text colour or stroked/curved vector API, so those scenarios have no baseline and no ratios.
- HTML to PDF has no eligible self-contained competitor, so it reports absolute UnlimitedPDF values only.
- Complex-script shaping is validated by text extraction and checked visually from the kept samples; there is no
  automated glyph-by-glyph shaping comparison.
- Native memory is visible only through process working set and private bytes; it is never derived by subtraction.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md), [AGENTS.md](AGENTS.md), the [code of conduct](CODE_OF_CONDUCT.md) and the
[security policy](SECURITY.md). New libraries and scenarios need equivalent inputs, an adapter using the public API,
independent validation and tests, and a licence review.

## License

[Apache 2.0](LICENSE). Third-party components and fonts: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
