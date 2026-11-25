using System.Globalization;
using System.Text.Json;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Provenance;

namespace UnlimitedPDF.Benchmarks.Reporting;

/// <summary>A validation record as written by the Runner (validation.json).</summary>
public sealed record CaseRecord(
    string CaseId,
    string Type,
    string Scenario,
    string Category,
    string Library,
    Dictionary<string, string> Parameters,
    ResultStatus Status,
    string? Reason,
    List<string> Failures,
    int PageCount,
    long OutputBytes,
    int OutputDocuments,
    double SingleRunMilliseconds,
    List<string> Validators,
    List<string> Warnings,
    string? SamplePdf,
    string? SampleImage);

/// <summary>BenchmarkDotNet statistics for one case. Times in nanoseconds per operation.</summary>
public sealed record Measurement(
    double MeanNs,
    double MedianNs,
    double MinNs,
    double MaxNs,
    double StdDevNs,
    double StdErrNs,
    double? P95Ns,
    double? Ci999MarginNs,
    int N,
    long? AllocatedBytesPerOperation,
    double Gen0Per1000,
    double Gen1Per1000,
    double Gen2Per1000,
    string Job);

/// <summary>One scenario/library/parameter row of the combined results, with ratios against the UnlimitedPDF baseline.</summary>
public sealed record ResultRow(
    string CaseId,
    string Category,
    string Scenario,
    string Type,
    string Library,
    string Parameters,
    ResultStatus Status,
    string? Reason,
    Measurement? Measurement,
    long OutputBytes,
    int PageCount,
    int OutputDocuments,
    double? TimeRatio,
    double? AllocationRatio,
    string? SampleImage,
    IReadOnlyList<string> Warnings)
{
    public string GroupKey => $"{Type}|{Parameters}";
}

public sealed class RunData
{
    public required string Folder { get; init; }
    public RunManifest? Manifest { get; init; }
    public required IReadOnlyList<CaseRecord> Cases { get; init; }
    public required IReadOnlyList<ResultRow> Rows { get; init; }
    public required IReadOnlyList<JsonElement> Profiler { get; init; }
    public JsonElement? BenchmarkHost { get; init; }

    public static RunData Load(string folder)
    {
        var manifestPath = Path.Combine(folder, "manifest.json");
        var manifest = File.Exists(manifestPath) ? RunManifest.Load(manifestPath) : null;
        var validationPath = Path.Combine(folder, "validation.json");
        var cases = File.Exists(validationPath)
            ? JsonSerializer.Deserialize<List<CaseRecord>>(File.ReadAllText(validationPath), RunManifest.JsonOptions) ?? []
            : [];

        var measurements = new Dictionary<string, Measurement>(StringComparer.Ordinal);
        JsonElement? host = null;
        var bdn = Path.Combine(folder, "bdn", "results");
        if (Directory.Exists(bdn))
        {
            foreach (var file in Directory.EnumerateFiles(bdn, "*-report-full.json").Order(StringComparer.Ordinal))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(file));
                host ??= json.RootElement.TryGetProperty("HostEnvironmentInfo", out var h) ? h.Clone() : null;
                foreach (var benchmark in json.RootElement.GetProperty("Benchmarks").EnumerateArray())
                {
                    var measurement = ParseMeasurement(benchmark);
                    if (measurement is null)
                        continue;
                    var parameters = ParseParameters(benchmark.GetProperty("Parameters").GetString());
                    var key = CaseKey(benchmark.GetProperty("Type").GetString()!, benchmark.GetProperty("Method").GetString()!, parameters);
                    measurements[key] = measurement;
                }
            }
        }

        var rows = new List<ResultRow>();
        foreach (var c in cases)
        {
            var parameters = FormatParameters(c.Parameters);
            var key = CaseKey(c.Type, c.Library, c.Parameters);
            var measured = measurements.GetValueOrDefault(key);
            var status = c.Status;
            var reason = c.Reason ?? (c.Failures.Count > 0 ? string.Join("; ", c.Failures.Take(3)) : null);
            if (status == ResultStatus.Passed && measured is null)
            {
                status = ResultStatus.NotExecuted;
                reason = "Passed validation but was not measured in this run (filtered out, or BenchmarkDotNet produced no result).";
            }
            rows.Add(new ResultRow(c.CaseId, c.Category, c.Scenario, c.Type, c.Library, parameters, status, reason, measured,
                c.OutputBytes, c.PageCount, c.OutputDocuments, null, null, c.SampleImage, c.Warnings));
        }

        // Ratios only against a passed, measured, positive UnlimitedPDF baseline in the same scenario and parameters.
        var baselines = rows.Where(r => r.Library == LibraryIds.Baseline && r.Status == ResultStatus.Passed && r.Measurement is not null)
            .ToDictionary(r => r.GroupKey, StringComparer.Ordinal);
        rows = rows.Select(r =>
        {
            if (r.Status != ResultStatus.Passed || r.Measurement is null || !baselines.TryGetValue(r.GroupKey, out var b))
                return r;
            return r with
            {
                TimeRatio = Statistics.Ratio(r.Measurement.MeanNs, b.Measurement!.MeanNs),
                AllocationRatio = Statistics.Ratio(r.Measurement.AllocatedBytesPerOperation, b.Measurement.AllocatedBytesPerOperation),
            };
        }).ToList();

        var profilerPath = Path.Combine(folder, "profiler", "results.json");
        var profiler = new List<JsonElement>();
        if (File.Exists(profilerPath))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(profilerPath));
            profiler.AddRange(json.RootElement.EnumerateArray().Select(e => e.Clone()));
        }

        return new RunData { Folder = folder, Manifest = manifest, Cases = cases, Rows = rows, Profiler = profiler, BenchmarkHost = host };
    }

    public static Measurement? ParseMeasurement(JsonElement benchmark)
    {
        if (!benchmark.TryGetProperty("Statistics", out var s) || s.ValueKind != JsonValueKind.Object)
            return null;
        double D(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;
        double? P95 = s.TryGetProperty("Percentiles", out var p) && p.ValueKind == JsonValueKind.Object ? D(p, "P95") : null;
        double? margin = s.TryGetProperty("ConfidenceInterval", out var ci) && ci.ValueKind == JsonValueKind.Object ? D(ci, "Margin") : null;
        long? allocated = null;
        double g0 = 0, g1 = 0, g2 = 0;
        if (benchmark.TryGetProperty("Memory", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            allocated = m.TryGetProperty("BytesAllocatedPerOperation", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt64() : null;
            var ops = D(m, "TotalOperations");
            if (ops > 0)
            {
                g0 = D(m, "Gen0Collections") / ops * 1000;
                g1 = D(m, "Gen1Collections") / ops * 1000;
                g2 = D(m, "Gen2Collections") / ops * 1000;
            }
        }
        var job = benchmark.TryGetProperty("DisplayInfo", out var di) ? di.GetString() ?? "" : "";
        var jobStart = job.LastIndexOf('[');
        return new Measurement(D(s, "Mean"), D(s, "Median"), D(s, "Min"), D(s, "Max"), D(s, "StandardDeviation"), D(s, "StandardError"),
            P95, margin, s.TryGetProperty("N", out var n) ? n.GetInt32() : 0, allocated, g0, g1, g2,
            jobStart >= 0 ? job[(jobStart + 1)..].TrimEnd(']') : "");
    }

    /// <summary>BenchmarkDotNet writes parameters as "A=1&amp;B=2".</summary>
    public static Dictionary<string, string> ParseParameters(string? text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
            return result;
        foreach (var part in text.Split(['&', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0)
                result[part[..eq].Trim()] = part[(eq + 1)..].Trim();
        }
        return result;
    }

    public static string CaseKey(string type, string method, IReadOnlyDictionary<string, string> parameters) =>
        $"{type}.{method}({FormatParameters(parameters)})";

    public static string FormatParameters(IReadOnlyDictionary<string, string> parameters) =>
        string.Join(", ", parameters.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
}

public static class Units
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Time(double? ns)
    {
        if (ns is not { } v || !double.IsFinite(v))
            return "-";
        return v switch
        {
            < 1_000 => v.ToString("0.0", Invariant) + " ns",
            < 1_000_000 => (v / 1_000).ToString("0.00", Invariant) + " μs",
            < 1_000_000_000 => (v / 1_000_000).ToString("0.00", Invariant) + " ms",
            _ => (v / 1_000_000_000).ToString("0.000", Invariant) + " s",
        };
    }

    public static string Milliseconds(double? ms) => ms is { } v && double.IsFinite(v) ? Time(v * 1_000_000) : "-";

    public static string Bytes(double? bytes)
    {
        if (bytes is not { } v || !double.IsFinite(v))
            return "-";
        return Math.Abs(v) switch
        {
            < 1024 => v.ToString("0", Invariant) + " B",
            < 1024 * 1024 => (v / 1024).ToString("0.00", Invariant) + " KiB",
            < 1024L * 1024 * 1024 => (v / 1024 / 1024).ToString("0.00", Invariant) + " MiB",
            _ => (v / 1024 / 1024 / 1024).ToString("0.00", Invariant) + " GiB",
        };
    }

    public static string Ratio(double? ratio) => ratio is { } r ? r.ToString("0.00", Invariant) + "×" : "-";

    public static string Number(double value, string format = "0.##") => value.ToString(format, Invariant);
}
