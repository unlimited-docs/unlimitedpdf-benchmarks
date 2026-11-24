# Contributing

Contributions that make the comparison fairer, more complete or easier to reproduce are welcome, especially from
maintainers and users of the libraries being compared. If you think a library is used incorrectly or inefficiently, open
an issue or a pull request with the improved adapter and the reasoning; the goal is each library's recommended
production usage, not a particular outcome.

## Before you start

- Read [AGENTS.md](AGENTS.md), [docs/methodology.md](docs/methodology.md) and [docs/architecture.md](docs/architecture.md).
- Install the .NET 10 SDK. Windows and Linux are supported.

## Workflow

```sh
dotnet restore UnlimitedPDF.Benchmarks.slnx --locked-mode
dotnet build UnlimitedPDF.Benchmarks.slnx -c Release --no-restore
dotnet test UnlimitedPDF.Benchmarks.slnx -c Release --no-build --no-restore
pwsh ./scripts/check-licenses.ps1
pwsh ./scripts/run-benchmarks.ps1 -Profile smoke -SkipBuild
```

When you change dependencies, run a normal `dotnet restore` to update the lock files and commit them.

## Adding a scenario

1. Add a deterministic fixture in `src/UnlimitedPDF.Benchmarks.Core/Fixtures` (no real or confidential data).
2. Add capability methods to the adapters of the libraries whose public API supports the scenario.
3. Add a benchmark class in `src/UnlimitedPDF.Benchmarks.Runner/Benchmarks` with a `[Scenario]` description, one
   `[Benchmark]` method per library (UnlimitedPDF as `Baseline` when it participates), `[NotApplicable]` with a reason for
   every other library, and an expectation in `Infrastructure/Expectations.cs` that a wrong output would fail.
4. Add sizes to each profile; keep the largest sizes in `stress`.
5. The integration tests run every case automatically; add unit tests for new fixture or validation logic.
6. Document the scenario in docs/benchmark-scenarios.md and any capability change in docs/feature-comparison.md.

## Adding a library

Follow the licensing policy in [docs/library-licenses.md](docs/library-licenses.md): verify the exact version's licence,
notices, native binaries and transitive packages, record the evidence there and in THIRD-PARTY-NOTICES.md, and keep
`scripts/check-licenses.ps1` passing. Then follow "Adding a library" in docs/architecture.md.

## Results

Do not commit raw run folders. To publish a run, add a Markdown summary under `docs/results/` stating the machine,
commit, profile and limitations, generated from the run's `summary.md`, and keep the raw folder available. Never edit or
estimate numbers.

## Pull requests

Keep changes focused, describe what changed and why, and include the commands you ran. CI runs the locked restore,
vulnerability and licence checks, build, unit and integration tests and a smoke run on Windows and Linux.
