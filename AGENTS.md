# Project instructions

Maintain a neutral, independently verifiable benchmark suite. UnlimitedPDF is the baseline, not a predetermined winner.
Use published packages and public APIs; do not optimize one implementation by disadvantaging another.

## Architecture

- **Core**: deterministic fixtures, the independent validator (PDFium via Docnet.Core, plus PdfPig/PDFsharp structure
  checks), profiles and run provenance. No benchmarked code paths live here.
- **Adapters**: one folder per library. An adapter implements only the capability interfaces (`ITextDocumentGenerator`,
  `ITableGenerator`, `IPdfMerger`, ...) its documented public API supports. Reusable objects (fonts, typefaces) are created
  in the constructor; one instance per thread.
- **Runner**: BenchmarkDotNet scenarios (one class per scenario, one `[Benchmark]` method per library, named by library id,
  UnlimitedPDF as `Baseline`), the validation pass and the filter that only lets validated cases be measured.
- **ResourceProfiler**: process-level measurements in isolated child processes.
- **Reporting**: regenerates all reports from raw run data.
- **Tests**: unit tests for fixtures, statistics and validator rejection; integration tests run every scenario and library.

## Rules

- Keep fixture generation, adapter construction and validation outside measured methods.
- Give every library identical inputs (same font files, image bytes, data, HTML, input PDFs) and equivalent output
  semantics. Document unavoidable differences in the adapter's XML comment and in docs/methodology.md.
- Declare non-participating libraries with `[NotApplicable(library, reason)]` and parameter-specific gaps with
  `UnsupportedReason`. Never emulate a missing feature with unrelated code or count it as a failure or a zero.
- Never rank failed, unsupported, timed-out or unexecuted cases. Ratios exist only against a passed baseline.
- Never fabricate, estimate or hand-edit measurements. Never conflate managed allocation with process or native memory.
- Use .NET 10, nullable C#, central package versions and lock files. Before adding or upgrading a package, review its
  licence, notices, native binaries and vulnerabilities for the exact version, update docs/library-licenses.md and
  THIRD-PARTY-NOTICES.md, and make `scripts/check-licenses.ps1` pass. Preserve the Apache 2.0 licence.
- Do not commit credentials, generated run folders or build output. Font, image and document assets must be generated or
  redistributable.

## Workflow

Run a locked restore, Release build and both test projects (see README) before finishing. For a new scenario add the
fixture to Core, the adapter methods, a benchmark class with an expectation in `Expectations`, profile sizes, and make
sure the integration tests cover it; document the scenario in docs/benchmark-scenarios.md and the capability in
docs/feature-comparison.md. Record raw results with their environment and document limitations and reproducible commands.
