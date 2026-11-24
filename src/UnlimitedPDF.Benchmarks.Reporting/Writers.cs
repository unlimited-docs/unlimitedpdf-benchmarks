using System.Globalization;
using System.Text;
using System.Text.Json;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Provenance;

namespace UnlimitedPDF.Benchmarks.Reporting;

/// <summary>Flat and structured exports. Every number comes from the raw files; nothing is estimated.</summary>
public static class DataWriters
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void WriteCsv(RunData data, string path)
    {
        var builder = new StringBuilder();
        builder.AppendLine("case_id,category,scenario,type,library,parameters,status,reason,mean_ns,median_ns,min_ns,max_ns,stddev_ns,stderr_ns,p95_ns,ci999_margin_ns,n," +
                           "allocated_bytes_per_op,gen0_per_1000,gen1_per_1000,gen2_per_1000,job,output_bytes,pages,output_documents,time_ratio,allocation_ratio");
        foreach (var r in data.Rows)
        {
            var m = r.Measurement;
            string N(double? v) => v is { } x && double.IsFinite(x) ? x.ToString("R", Invariant) : "";
            builder.AppendJoin(',', [
                Csv(r.CaseId), Csv(r.Category), Csv(r.Scenario), Csv(r.Type), Csv(r.Library), Csv(r.Parameters), r.Status.ToString(), Csv(r.Reason),
                N(m?.MeanNs), N(m?.MedianNs), N(m?.MinNs), N(m?.MaxNs), N(m?.StdDevNs), N(m?.StdErrNs), N(m?.P95Ns), N(m?.Ci999MarginNs),
                m?.N.ToString(Invariant) ?? "", m?.AllocatedBytesPerOperation?.ToString(Invariant) ?? "", N(m?.Gen0Per1000), N(m?.Gen1Per1000),
                N(m?.Gen2Per1000), Csv(m?.Job), r.OutputBytes.ToString(Invariant), r.PageCount.ToString(Invariant), r.OutputDocuments.ToString(Invariant),
                N(r.TimeRatio), N(r.AllocationRatio)]);
            builder.AppendLine();
        }
        File.WriteAllText(path, builder.ToString());
    }

    public static void WriteJson(RunData data, string path)
    {
        var document = new
        {
            generatedUtc = DateTimeOffset.UtcNow,
            manifest = data.Manifest,
            definitions = new
            {
                timeRatio = "competitor mean time / UnlimitedPDF mean time for the same scenario and parameters; > 1 means more time",
                allocationRatio = "competitor managed bytes allocated per operation / UnlimitedPDF's; > 1 means more managed allocation",
                allocated = "cumulative managed allocations per operation (BenchmarkDotNet MemoryDiagnoser), not retained, peak or native memory",
            },
            rows = data.Rows,
            profiler = data.Profiler,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(document, RunManifest.JsonOptions));
    }

    public static void WriteMarkdown(RunData data, string path)
    {
        var md = new StringBuilder();
        var manifest = data.Manifest;
        md.AppendLine($"# Benchmark summary: run {manifest?.RunId ?? Path.GetFileName(data.Folder)}");
        md.AppendLine();
        md.AppendLine("Generated from the raw files in this run folder by UnlimitedPDF.Benchmarks.Reporting. Lower is better for time and allocation.");
        md.AppendLine("Ratios divide each library's value by UnlimitedPDF's for the same scenario and parameters and are shown only when that baseline passed validation and was measured.");
        md.AppendLine("Allocated is cumulative managed allocation per operation, not retained, peak or native memory. Failed, unsupported and unexecuted cases have no numbers.");
        md.AppendLine();
        if (manifest is not null)
        {
            var e = manifest.Environment;
            md.AppendLine($"- Started: {manifest.StartedUtc:yyyy-MM-dd HH:mm} UTC; profile: {manifest.Profile}; commit: `{manifest.Git.Commit ?? "unknown"}`{(manifest.Git.Dirty == true ? " (uncommitted changes)" : "")}");
            md.AppendLine($"- {e.OperatingSystem}, {e.CpuModel}, {e.LogicalCpuCount} logical CPUs, {Units.Bytes(e.TotalMemoryBytes)} RAM, {e.RuntimeVersion}, SDK {e.SdkVersion}, {e.GcMode} GC");
            md.AppendLine($"- Fixtures {manifest.FixtureVersion}, SHA-256 `{manifest.FixtureSha256}`");
            md.AppendLine();
        }

        md.AppendLine("## Validation status");
        md.AppendLine();
        md.AppendLine("Case counts. Unsupported includes one entry per scenario a library takes no part in.");
        md.AppendLine();
        md.AppendLine("| Library | Passed | Failed validation | Execution failed | Timed out | Unsupported | Not executed |");
        md.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (var library in LibraryIds.All)
        {
            var rows = data.Rows.Where(r => r.Library == library).ToArray();
            int C(ResultStatus s) => rows.Count(r => r.Status == s);
            md.AppendLine($"| {Name(library)} | {C(ResultStatus.Passed)} | {C(ResultStatus.FailedValidation)} | {C(ResultStatus.ExecutionFailed)} | {C(ResultStatus.TimedOut)} | {C(ResultStatus.Unsupported)} | {C(ResultStatus.NotExecuted)} |");
        }
        md.AppendLine();

        foreach (var category in data.Rows.Select(r => r.Category).Distinct())
        {
            md.AppendLine($"## {category}");
            md.AppendLine();
            foreach (var type in data.Rows.Where(r => r.Category == category).Select(r => r.Type).Distinct())
            {
                var typeRows = data.Rows.Where(r => r.Type == type).ToArray();
                md.AppendLine($"### {typeRows[0].Scenario}");
                md.AppendLine();
                var notApplicable = typeRows.Where(r => r.CaseId.EndsWith("(*)", StringComparison.Ordinal)).ToArray();
                if (notApplicable.Length > 0)
                    md.AppendLine("Not applicable: " + string.Join("; ", notApplicable.Select(r => $"{Name(r.Library)} ({r.Reason})")) + ".").AppendLine();
                foreach (var group in typeRows.Where(r => !r.CaseId.EndsWith("(*)", StringComparison.Ordinal)).GroupBy(r => r.Parameters))
                {
                    md.AppendLine($"**{(group.Key.Length == 0 ? "No parameters" : group.Key)}**");
                    md.AppendLine();
                    md.AppendLine("| Library | Status | Mean | ± 99.9% CI | Median | P95 | Allocated/op | Gen0/1k | Output | Pages | Time ratio | Alloc ratio |");
                    md.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
                    foreach (var r in group)
                    {
                        var m = r.Measurement;
                        var passed = r.Status == ResultStatus.Passed;
                        md.AppendLine($"| {Name(r.Library)} | {StatusText(r.Status)} | {(passed ? Units.Time(m?.MeanNs) : "-")} | {(passed ? Units.Time(m?.Ci999MarginNs) : "-")} | " +
                                      $"{(passed ? Units.Time(m?.MedianNs) : "-")} | {(passed ? Units.Time(m?.P95Ns) : "-")} | {(passed ? Units.Bytes(m?.AllocatedBytesPerOperation) : "-")} | " +
                                      $"{(passed && m is not null ? Units.Number(m.Gen0Per1000, "0.0") : "-")} | {(passed ? Units.Bytes(r.OutputBytes) : "-")} | {(passed ? r.PageCount.ToString(Invariant) : "-")} | " +
                                      $"{Units.Ratio(r.TimeRatio)} | {Units.Ratio(r.AllocationRatio)} |");
                    }
                    var notes = group.Where(r => r.Status != ResultStatus.Passed && r.Reason is not null).ToArray();
                    if (notes.Length > 0)
                    {
                        md.AppendLine();
                        foreach (var r in notes)
                            md.AppendLine($"- {Name(r.Library)}: {StatusText(r.Status)}: {r.Reason}");
                    }
                    md.AppendLine();
                }
            }
        }

        ProfilerMarkdown(data, md);
        File.WriteAllText(path, md.ToString());
    }

    private static void ProfilerMarkdown(RunData data, StringBuilder md)
    {
        if (data.Profiler.Count == 0)
            return;
        md.AppendLine("## Process-level resource profile");
        md.AppendLine();
        md.AppendLine("Measured by UnlimitedPDF.Benchmarks.ResourceProfiler, one fresh process per measurement. Working set and private bytes include native memory and runtime overhead; managed allocation is cumulative.");
        md.AppendLine();
        foreach (var workload in data.Profiler.Select(p => S(p, "workload")).Distinct())
        {
            md.AppendLine($"### Workload `{workload}`");
            md.AppendLine();
            var cold = data.Profiler.Where(p => S(p, "workload") == workload && S(p, "suite") == "ColdStart").GroupBy(p => S(p, "library")).ToArray();
            if (cold.Length > 0)
            {
                md.AppendLine("Cold start (median of runs; fresh process each):");
                md.AppendLine();
                md.AppendLine("| Library | Runs | Status | Process start → first PDF | Library init | First document | Peak working set |");
                md.AppendLine("|---|---:|---|---:|---:|---:|---:|");
                foreach (var g in cold)
                {
                    var passed = g.Where(p => S(p, "status") == "Passed").ToArray();
                    md.AppendLine($"| {Name(g.Key)} | {g.Count()} | {(passed.Length == g.Count() ? "Passed" : string.Join(", ", g.Select(p => S(p, "status")).Distinct()))} | " +
                                  $"{Units.Milliseconds(Median(passed, "processStartToFirstPdfMs"))} | {Units.Milliseconds(Median(passed, "libraryInitializationMs"))} | " +
                                  $"{Units.Milliseconds(Median(passed, "firstDocumentMs"))} | {Units.Bytes(Median(passed, "peakWorkingSetBytes"))} |");
                }
                md.AppendLine();
            }
            var repeated = data.Profiler.Where(p => S(p, "workload") == workload && S(p, "suite") != "ColdStart").ToArray();
            if (repeated.Length > 0)
            {
                md.AppendLine("| Suite | Library | Docs | Workers | Status | Docs/s | P50 | P95 | P99 | CPU % | Alloc/doc | Peak WS | Peak private | Retained heap |");
                md.AppendLine("|---|---|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
                foreach (var p in repeated)
                {
                    var passed = S(p, "status") == "Passed";
                    var latency = p.TryGetProperty("latency", out var l) && l.ValueKind == JsonValueKind.Object ? l : (JsonElement?)null;
                    md.AppendLine($"| {S(p, "suite")} | {Name(S(p, "library"))} | {I(p, "documents")} | {I(p, "workers")} | {S(p, "status")} | " +
                                  $"{(passed ? Units.Number(D(p, "documentsPerSecond"), "0.0") : "-")} | {(passed ? Units.Milliseconds(latency?.GetProperty("p50Ms").GetDouble()) : "-")} | " +
                                  $"{(passed ? Units.Milliseconds(latency?.GetProperty("p95Ms").GetDouble()) : "-")} | {(passed ? Units.Milliseconds(latency?.GetProperty("p99Ms").GetDouble()) : "-")} | " +
                                  $"{(passed ? Units.Number(D(p, "cpuUtilizationPercent"), "0") : "-")} | {(passed ? Units.Bytes(D(p, "managedAllocatedBytesPerDocument")) : "-")} | " +
                                  $"{(passed ? Units.Bytes(D(p, "peakWorkingSetBytes")) : "-")} | {(passed ? Units.Bytes(D(p, "peakPrivateBytes")) : "-")} | {(passed ? Units.Bytes(D(p, "retainedManagedHeapBytes")) : "-")} |");
                }
                md.AppendLine();
            }
        }
    }

    internal static string S(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.ToString() : "";

    internal static double D(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;

    internal static int I(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    internal static double? Median(IReadOnlyList<JsonElement> items, string name)
    {
        var values = items.Select(i => D(i, name)).Where(double.IsFinite).Order().ToList();
        return values.Count == 0 ? null : Statistics.Percentile(values, 50);
    }

    public static string Name(string library) => LibraryCatalog.All.FirstOrDefault(l => l.Id == library)?.DisplayName ?? library;

    public static string StatusText(ResultStatus status) => status switch
    {
        ResultStatus.Passed => "Passed",
        ResultStatus.FailedValidation => "Failed validation",
        ResultStatus.Unsupported => "Unsupported (N/A)",
        ResultStatus.ExecutionFailed => "Execution failed",
        ResultStatus.TimedOut => "Timed out",
        _ => "Not executed",
    };

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    }
}
