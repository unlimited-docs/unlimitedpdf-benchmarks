# Methodology and limitations

Each timed method constructs a new document with the same number of blank A4
pages and fully serializes it to a byte array in memory. There is no file I/O,
font, image, layout, validation or fixture generation in the timed operation.
The public library APIs and their defaults are used. Byte-array construction
and serialization are included; PdfPig directly returns bytes, while the
other two serialize into a memory stream and copy to an array. This difference
is part of the chosen public API path and must be considered when interpreting
results. Defaults, PDF metadata, compression and internal representation can
differ. Blank pages are only a very narrow workload.

Global setup generates representative outputs and checks page count and
dimensions with PdfPig for UnlimitedPDF and PDFsharp and PDFsharp for PdfPig.
Failure aborts the benchmark; it is not a zero result. This check is not a
complete PDF conformance, rendering or visual-quality test. Tests check both
page counts and rejection of incorrect counts. No malformed-file, content,
fonts, images, text, page ordering or visual assertions have been implemented.

BenchmarkDotNet runs in Release, warms up by default, records its environment
and uses MemoryDiagnoser for managed allocations and collection counts. A
`Dry` job is a smoke test, not a representative measurement. Results contain
absolute time and allocation values; the execution-time ratio is competitor
time divided by UnlimitedPDF time, and allocation ratio is competitor managed
allocation divided by UnlimitedPDF allocation. Greater than one means more
time or allocation; lower is better for these metrics. Do not compare ratios
if the baseline is invalid, missing or zero. Confidence and variability must
be considered, and unrelated workloads must not be combined into an overall
score.

Allocated bytes are cumulative managed allocations per operation, not retained
memory or peak working set. Garbage collection counts describe collections,
not leaks. Native allocation, process working set, private bytes, CPU, browser
children, retained heap, throughput, individual-request percentiles, cold
start, concurrency and long-running behavior are **not measured** here.
Iteration statistics are not request latency. In particular, do not estimate
native memory by subtracting the managed heap from working set.

Run on controlled hardware without a debugger; preserve all raw outputs, exact
commit, lock files and machine details. BenchmarkDotNet records some environment
details, but automatic unique run IDs, commit hashes, fixture checksums,
complete hardware metadata and result regeneration are not yet implemented.
Do not compare materially different machines or runtimes without qualification.
