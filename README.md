# UnlimitedPDF.Benchmarks

An open, reproducible comparison of the published UnlimitedPDF package with
commercially usable open-source PDF libraries. This repository is associated
with UnlimitedPDF; independent contributors and maintainers of other libraries
are encouraged to review the implementations and methodology.

## Current status

This is an **initial foundation**, not the complete benchmark suite. It
compares in-memory creation and serialization of 1 and 10 blank A4 pages using
UnlimitedPDF 1.1.0, PDFsharp 6.2.4 and PdfPig 0.1.16. An independent parser
checks page count and dimensions before timing. Empty pages do not establish
correctness or comparative performance for text, graphics, tables or any other
feature. No overall winner can be inferred.

See [licenses](docs/library-licenses.md) for the licensing policy, evidence
and important PDFsharp version caveat. The matrix and unimplemented scenarios
are in [feature comparison](docs/feature-comparison.md); measurement caveats
are in [methodology](docs/methodology.md).

## Getting started

Install the .NET 10 SDK. Windows and Linux are intended targets. From the
repository root:

```sh
dotnet restore UnlimitedPDF.Benchmarks.slnx --locked-mode
dotnet build UnlimitedPDF.Benchmarks.slnx -c Release --no-restore
dotnet test UnlimitedPDF.Benchmarks.slnx -c Release --no-build --no-restore
dotnet run --project src/UnlimitedPDF.Benchmarks.Runner -c Release --no-build -- --filter '*EmptyPageBenchmarks*' --job short
```

PowerShell: `./scripts/benchmark.ps1 --filter '*EmptyPageBenchmarks*' --job short`.
Bash: `bash scripts/benchmark.sh --filter '*EmptyPageBenchmarks*' --job short`.
Use `--job Dry` for a functional smoke run (not meaningful performance data),
or omit `--job` for BenchmarkDotNet's default measurements. See `--help` for
BenchmarkDotNet filters and other options. There are not yet dedicated
smoke/quick/standard/full/stress profiles.

BenchmarkDotNet writes raw logs and Markdown, CSV, HTML and JSON exports to
`BenchmarkDotNet.Artifacts/`, which is ignored by git. Preserve the whole
directory and record the commit and environment when sharing results. Results
from GitHub-hosted runners are informational; use controlled hardware for
authoritative comparisons. No benchmark results are shipped or fabricated.

## Architecture and contributions

The Core project contains generation and independent validation, Runner contains
the BenchmarkDotNet workloads, and Tests verifies outputs. For a new workload,
define equivalent inputs and semantics, implement each library with its public
API, validate with an independent parser outside the timed method, and add
tests before adding it to comparisons. Unsupported or unverified features are
not benchmarked. Contributors should read [AGENTS.md](AGENTS.md) and
[methodology](docs/methodology.md); dependency upgrades require renewed license
and vulnerability review. Repository license: [Apache 2.0](LICENSE).
