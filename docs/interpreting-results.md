# Interpreting results

## Read one scenario at a time

Each table compares libraries doing the same work with the same inputs and the same parameters. Never add or average
numbers across scenarios: a library that is fastest at blank pages can be slowest at tables. The suite deliberately has no
overall score or winner.

## Time

- **Mean** is the average time per operation over BenchmarkDotNet's measurement iterations; **± 99.9% CI** is half the
  99.9% confidence interval. If two libraries' intervals overlap, treat them as indistinguishable in that run.
- **Median**, **P95** and **StdDev** describe the spread of iteration means. They are *not* request latency: an iteration
  averages many invocations. Per-document latency percentiles come from the resource profiler.
- BenchmarkDotNet warnings (multimodal distributions, removed outliers) are in the raw logs under `bdn/`; consult them
  before relying on small differences.

## Ratios

Time ratio = competitor mean / UnlimitedPDF mean; allocation ratio = competitor allocated bytes per operation /
UnlimitedPDF's. 2.00× means twice the time or managed allocation of UnlimitedPDF; 0.50× means half. Ratios appear only
when UnlimitedPDF passed validation and was measured for the same parameters. Scenarios UnlimitedPDF does not support
(coloured text, vector graphics) have no ratios; compare their absolute values.

## Memory

- **Allocated/op** counts every managed byte allocated during an operation, including garbage collected moments later.
  It is a good proxy for GC pressure; it is not how much memory a process needs, and it excludes native memory (for
  example inside Skia, HarfBuzz or PDFium).
- **Peak working set** and **private bytes** (profiler) are whole-process OS values that do include native memory, the
  .NET runtime and loaded assemblies. Compare them only between runs of the same suite and size.
- **Retained heap** is managed memory still alive after the workload and full collections. Growth across a long run is an
  observation that needs a profiler to explain; it is not reported as a leak.

## Output size and pages

Output size depends on library defaults (font subsetting, stream compression, metadata). SkiaSharp 4.157.0, for example,
embeds large font programs, so its text outputs are much larger. Layout libraries may paginate differently (different
line heights and cell padding), so page counts in table and business-document scenarios can differ while all content is
present.

## Statuses

| Status | Meaning |
|---|---|
| Passed | Output passed independent validation and was measured. |
| Failed validation | The library produced output that is missing content, has wrong pages or is unreadable. Not measured or ranked. |
| Unsupported (N/A) | The capability is not offered by the library's public API. Not a failure. |
| Execution failed | The library threw an exception. |
| Timed out | No output within the validation timeout. |
| Not executed | Supported but not run here (for example filtered out, or not measured by BenchmarkDotNet). |

## Comparing runs

Only compare runs from the same machine, OS, runtime, power plan and commit (`manifest.json`). Check the fixture checksum:
different fixtures mean different work. Results from GitHub-hosted runners vary with shared hardware and are informational.
