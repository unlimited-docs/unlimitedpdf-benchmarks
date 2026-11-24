using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using UnlimitedPDF.Benchmarks.Core;
using static UnlimitedPDF.Benchmarks.Reporting.DataWriters;

namespace UnlimitedPDF.Benchmarks.Reporting;

/// <summary>
/// A single self-contained, accessible HTML report: inline CSS and SVG only, no scripts or external requests. Every chart has
/// a table with the same numbers next to it, and identity is always given by text labels as well as colour.
/// </summary>
public static class HtmlReport
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, int> Slot = new()
    {
        [LibraryIds.UnlimitedPdf] = 1,
        [LibraryIds.PdfSharp] = 2,
        [LibraryIds.MigraDoc] = 3,
        [LibraryIds.PdfPig] = 4,
        [LibraryIds.SkiaSharp] = 5,
    };

    private const string Css = """
        :root{color-scheme:light;--surface:#fcfcfb;--surface-2:#f3f2ef;--border:#dedcd6;--text:#0b0b0b;--text-2:#52514e;--muted:#6f6e69;
        --s1:#2a78d6;--s2:#eb6834;--s3:#1baf7a;--s4:#eda100;--s5:#e87ba4;--s6:#008300;--good:#0b7a32;--bad:#b3261e;--warn:#8a5a00}
        @media (prefers-color-scheme:dark){:root:not([data-theme="light"]){color-scheme:dark;--surface:#1a1a19;--surface-2:#242422;--border:#3a3a37;
        --text:#f0efec;--text-2:#c3c2b7;--muted:#9a998f;--s1:#3987e5;--s2:#d95926;--s3:#199e70;--s4:#c98500;--s5:#d55181;--s6:#0ca30c;--good:#4cc27a;--bad:#f08a80;--warn:#e0b050}}
        :root[data-theme="dark"]{color-scheme:dark;--surface:#1a1a19;--surface-2:#242422;--border:#3a3a37;--text:#f0efec;--text-2:#c3c2b7;--muted:#9a998f;
        --s1:#3987e5;--s2:#d95926;--s3:#199e70;--s4:#c98500;--s5:#d55181;--s6:#0ca30c;--good:#4cc27a;--bad:#f08a80;--warn:#e0b050}
        *{box-sizing:border-box}body{margin:0;background:var(--surface);color:var(--text);font:15px/1.5 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
        main{max-width:1200px;margin:0 auto;padding:24px 16px 64px}h1{font-size:28px;margin:0 0 4px}h2{font-size:22px;margin:40px 0 8px;border-bottom:1px solid var(--border);padding-bottom:6px}
        h3{font-size:17px;margin:28px 0 4px}h4{font-size:14px;margin:18px 0 6px;color:var(--text-2)}p,li{color:var(--text-2)}a{color:var(--s1)}
        .card{background:var(--surface-2);border:1px solid var(--border);border-radius:10px;padding:12px 16px;margin:12px 0}
        .grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:12px}
        table{border-collapse:collapse;width:100%;font-size:13px;margin:6px 0 10px}caption{text-align:left;color:var(--muted);font-size:12px;padding:2px 0}
        th,td{border-bottom:1px solid var(--border);padding:4px 8px;text-align:right;white-space:nowrap}th:first-child,td:first-child{text-align:left}
        td.l,th.l{text-align:left;white-space:normal}thead th{color:var(--text-2);font-weight:600}.scroll{overflow-x:auto}
        .sw{display:inline-block;width:10px;height:10px;border-radius:2px;margin-right:6px;vertical-align:baseline}
        .st-Passed{color:var(--good)}.st-FailedValidation,.st-ExecutionFailed,.st-TimedOut{color:var(--bad)}.st-Unsupported,.st-NotExecuted{color:var(--muted)}
        svg text{fill:var(--text-2);font-size:12px}svg .v{fill:var(--text);font-size:11px}figure{margin:0}figcaption{font-size:12px;color:var(--muted)}
        .note{font-size:13px;color:var(--muted)}details{margin:6px 0}summary{cursor:pointer;color:var(--text-2)}
        .thumbs{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:10px}.thumbs img{width:100%;border:1px solid var(--border);background:#fff}
        .thumbs figcaption{font-size:11px}nav ul{columns:3 220px;padding-left:18px}code{font-size:12px}
        """;

    public static void Write(RunData data, string path)
    {
        var h = new StringBuilder();
        var manifest = data.Manifest;
        h.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        h.Append("<title>PDF Library Benchmarks</title><style>").Append(Css).Append("</style></head><body><main>");
        h.Append("<h1>PDF library benchmarks</h1>");
        h.Append("<p>Run <code>").Append(E(manifest?.RunId ?? Path.GetFileName(data.Folder))).Append("</code>");
        if (manifest is not null)
            h.Append(", started ").Append(manifest.StartedUtc.ToString("yyyy-MM-dd HH:mm", Invariant)).Append(" UTC, profile <b>").Append(E(manifest.Profile)).Append("</b>");
        h.Append(". UnlimitedPDF is the baseline, not a presumed winner. Lower is better for every time and memory value. ");
        h.Append("Each workload is reported on its own; there is no overall score.</p>");
        Disclosure(h);
        Provenance(h, data);
        Toc(h, data);
        StatusOverview(h, data);
        FeatureMatrix(h, data);
        foreach (var category in data.Rows.Select(r => r.Category).Distinct())
            Category(h, data, category);
        Profiler(h, data);
        Samples(h, data);
        Licensing(h);
        Methodology(h);
        h.Append("</main></body></html>");
        File.WriteAllText(path, h.ToString());
    }

    private static void Disclosure(StringBuilder h) =>
        h.Append("<div class=\"card\"><b>Read this first.</b> This suite is associated with UnlimitedPDF and benchmarks its published NuGet package. ")
         .Append("Other library names and trademarks belong to their owners; their maintainers are invited to review the implementations. ")
         .Append("Only outputs that passed independent validation (PDFium page count, page size, extracted text and a rendered page, plus font and image checks) were measured. ")
         .Append("<i>Allocated</i> is cumulative managed allocation per operation from BenchmarkDotNet's MemoryDiagnoser: it is not retained memory, peak memory or native memory. ")
         .Append("Ratios are competitor ÷ UnlimitedPDF for the same scenario and parameters, shown only when that baseline passed.</div>");

    private static void Provenance(StringBuilder h, RunData data)
    {
        var m = data.Manifest;
        if (m is null)
            return;
        var e = m.Environment;
        h.Append("<h2 id=\"environment\">Environment and provenance</h2><div class=\"grid\"><div class=\"card\"><table><tbody>");
        Row(h, "Commit", (m.Git.Commit ?? "unknown") + (m.Git.Dirty == true ? " (uncommitted changes)" : ""));
        Row(h, "Operating system", $"{e.OperatingSystem} ({e.OsArchitecture})");
        Row(h, "CPU", $"{e.CpuModel}; {e.LogicalCpuCount} logical{(e.PhysicalCpuCount is { } p ? $", {p} physical" : "")}");
        Row(h, "Memory", Units.Bytes(e.TotalMemoryBytes));
        Row(h, "Runtime / SDK", $"{e.RuntimeVersion} / {e.SdkVersion}");
        Row(h, "GC", $"{e.GcMode}, concurrent {e.ConcurrentGc}, {e.GcLatencyMode}");
        Row(h, "Build", $"{e.BuildConfiguration}, debugger attached: {e.DebuggerAttached}");
        if (e.PowerPlan is not null)
            Row(h, "Power plan", e.PowerPlan);
        Row(h, "Fixtures", $"{m.FixtureVersion}, SHA-256 {m.FixtureSha256[..16]}…");
        h.Append("</tbody></table></div><div class=\"card\"><table><thead><tr><th>Component</th><th>Version</th></tr></thead><tbody>");
        foreach (var c in m.Components)
            h.Append("<tr><td>").Append(E(c.Name)).Append("</td><td>").Append(E(c.Version)).Append("</td></tr>");
        if (data.BenchmarkHost is { } host)
            h.Append("<tr><td>BenchmarkDotNet host</td><td>").Append(E(S(host, "BenchmarkDotNetVersion"))).Append("</td></tr>");
        h.Append("</tbody></table></div></div>");
    }

    private static void Row(StringBuilder h, string label, string value) =>
        h.Append("<tr><td>").Append(E(label)).Append("</td><td class=\"l\">").Append(E(value)).Append("</td></tr>");

    private static void Toc(StringBuilder h, RunData data)
    {
        h.Append("<nav aria-label=\"Contents\"><ul><li><a href=\"#status\">Validation status</a></li><li><a href=\"#matrix\">Feature support matrix</a></li>");
        foreach (var type in data.Rows.Select(r => (r.Type, r.Scenario)).Distinct())
            h.Append("<li><a href=\"#").Append(type.Type).Append("\">").Append(E(type.Scenario)).Append("</a></li>");
        h.Append("<li><a href=\"#profiler\">Process-level profile</a></li><li><a href=\"#samples\">Output samples</a></li><li><a href=\"#licensing\">Licensing</a></li><li><a href=\"#methodology\">Methodology</a></li></ul></nav>");
    }

    private static void StatusOverview(StringBuilder h, RunData data)
    {
        h.Append("<h2 id=\"status\">Validation status</h2><p>Every case is executed once and validated before measurement. ")
         .Append("Unsupported cases are features a library does not offer through its public API (one entry per scenario it takes no part in, plus parameter-specific cases); they are not failures and have no numbers.</p><div class=\"scroll\"><table><thead><tr><th>Library</th>");
        foreach (var s in Enum.GetValues<ResultStatus>())
            h.Append("<th>").Append(StatusText(s)).Append("</th>");
        h.Append("</tr></thead><tbody>");
        foreach (var library in LibraryIds.All)
        {
            var rows = data.Rows.Where(r => r.Library == library).ToArray();
            h.Append("<tr><td>").Append(Swatch(library)).Append(E(Name(library))).Append("</td>");
            foreach (var s in Enum.GetValues<ResultStatus>())
                h.Append("<td>").Append(rows.Count(r => r.Status == s)).Append("</td>");
            h.Append("</tr>");
        }
        h.Append("</tbody></table></div>");
        var problems = data.Rows.Where(r => r.Status is ResultStatus.FailedValidation or ResultStatus.ExecutionFailed or ResultStatus.TimedOut).ToArray();
        if (problems.Length > 0)
        {
            h.Append("<h3>Failed, errored or timed-out cases (excluded from all results)</h3><div class=\"scroll\"><table><thead><tr><th>Case</th><th class=\"l\">Status</th><th class=\"l\">Reason</th></tr></thead><tbody>");
            foreach (var r in problems)
                h.Append("<tr><td>").Append(E(r.CaseId)).Append("</td><td class=\"l st-").Append(r.Status).Append("\">").Append(StatusText(r.Status))
                 .Append("</td><td class=\"l\">").Append(E(r.Reason ?? "")).Append("</td></tr>");
            h.Append("</tbody></table></div>");
        }
    }

    private static void FeatureMatrix(StringBuilder h, RunData data)
    {
        h.Append("<h2 id=\"matrix\">Feature support matrix (this run)</h2><p class=\"note\">Derived from the validation outcomes: ✓ every executed case passed; ✗ at least one failed; ")
         .Append("◐ supported for some parameters only; N/A not offered by the public API; – not executed on this machine. See docs/feature-comparison.md for the researched capability matrix.</p>")
         .Append("<div class=\"scroll\"><table><thead><tr><th>Scenario</th>");
        foreach (var library in LibraryIds.All)
            h.Append("<th>").Append(E(Name(library))).Append("</th>");
        h.Append("</tr></thead><tbody>");
        foreach (var type in data.Rows.Select(r => (r.Type, r.Scenario)).Distinct())
        {
            h.Append("<tr><td>").Append(E(type.Scenario)).Append("</td>");
            foreach (var library in LibraryIds.All)
            {
                var rows = data.Rows.Where(r => r.Type == type.Type && r.Library == library).ToArray();
                string cell;
                if (rows.Length == 0)
                    cell = "<span class=\"st-NotExecuted\">–</span>";
                else if (rows.Any(r => r.Status is ResultStatus.FailedValidation or ResultStatus.ExecutionFailed or ResultStatus.TimedOut))
                    cell = "<span class=\"st-FailedValidation\">✗ failed</span>";
                else if (rows.All(r => r.Status == ResultStatus.Unsupported))
                    cell = "<span class=\"st-Unsupported\">N/A</span>";
                else if (rows.All(r => r.Status == ResultStatus.NotExecuted))
                    cell = "<span class=\"st-NotExecuted\">– not run</span>";
                else if (rows.Any(r => r.Status == ResultStatus.Unsupported))
                    cell = "<span class=\"st-Passed\">◐ partial</span>";
                else
                    cell = "<span class=\"st-Passed\">✓ passed</span>";
                h.Append("<td>").Append(cell).Append("</td>");
            }
            h.Append("</tr>");
        }
        h.Append("</tbody></table></div>");
    }

    private static void Category(StringBuilder h, RunData data, string category)
    {
        h.Append("<h2>").Append(E(category)).Append("</h2>");
        foreach (var type in data.Rows.Where(r => r.Category == category).Select(r => r.Type).Distinct())
        {
            var typeRows = data.Rows.Where(r => r.Type == type).ToArray();
            h.Append("<h3 id=\"").Append(type).Append("\">").Append(E(typeRows[0].Scenario)).Append("</h3>");
            var na = typeRows.Where(IsWildcard).ToArray();
            if (na.Length > 0)
            {
                h.Append("<p class=\"note\">Not applicable: ");
                h.AppendJoin("; ", na.Select(r => $"{E(Name(r.Library))} ({E(r.Reason ?? "")})"));
                h.Append(".</p>");
            }
            foreach (var group in typeRows.Where(r => !IsWildcard(r)).GroupBy(r => r.Parameters))
            {
                var rows = group.ToArray();
                h.Append("<h4>").Append(E(group.Key.Length == 0 ? "No parameters" : group.Key)).Append("</h4>");
                var passed = rows.Where(r => r.Status == ResultStatus.Passed && r.Measurement is not null).ToArray();
                if (passed.Length > 0)
                {
                    h.Append("<div class=\"grid\">");
                    BarChart(h, $"Mean time per operation, {group.Key}", passed.Select(r => (r.Library, r.Measurement!.MeanNs, Units.Time(r.Measurement.MeanNs))).ToArray());
                    if (passed.Any(r => r.Measurement!.AllocatedBytesPerOperation is not null))
                        BarChart(h, $"Managed allocation per operation, {group.Key}",
                            passed.Select(r => (r.Library, (double)(r.Measurement!.AllocatedBytesPerOperation ?? 0), Units.Bytes(r.Measurement.AllocatedBytesPerOperation))).ToArray());
                    h.Append("</div>");
                }
                h.Append("<div class=\"scroll\"><table><caption>").Append(E(rows[0].Scenario)).Append(", ").Append(E(group.Key)).Append("</caption><thead><tr>")
                 .Append("<th>Library</th><th class=\"l\">Status</th><th>Mean</th><th>±99.9% CI</th><th>Median</th><th>P95</th><th>StdDev</th><th>Allocated/op</th>")
                 .Append("<th>Gen0/1k</th><th>Gen1/1k</th><th>Gen2/1k</th><th>Output size</th><th>Pages</th><th>Time ratio</th><th>Alloc ratio</th></tr></thead><tbody>");
                foreach (var r in rows)
                {
                    var m = r.Status == ResultStatus.Passed ? r.Measurement : null;
                    h.Append("<tr><td>").Append(Swatch(r.Library)).Append(E(Name(r.Library))).Append("</td><td class=\"l st-").Append(r.Status).Append("\">")
                     .Append(StatusText(r.Status));
                    if (r.Status != ResultStatus.Passed && r.Reason is not null)
                        h.Append("<br><span class=\"note\">").Append(E(r.Reason)).Append("</span>");
                    if (r.Warnings.Count > 0)
                        h.Append("<br><span class=\"note\">Warnings: ").Append(E(string.Join("; ", r.Warnings.Take(3)))).Append("</span>");
                    h.Append("</td><td>").Append(Units.Time(m?.MeanNs)).Append("</td><td>").Append(Units.Time(m?.Ci999MarginNs)).Append("</td><td>")
                     .Append(Units.Time(m?.MedianNs)).Append("</td><td>").Append(Units.Time(m?.P95Ns)).Append("</td><td>").Append(Units.Time(m?.StdDevNs))
                     .Append("</td><td>").Append(Units.Bytes(m?.AllocatedBytesPerOperation)).Append("</td><td>").Append(m is null ? "-" : Units.Number(m.Gen0Per1000, "0.0"))
                     .Append("</td><td>").Append(m is null ? "-" : Units.Number(m.Gen1Per1000, "0.0")).Append("</td><td>").Append(m is null ? "-" : Units.Number(m.Gen2Per1000, "0.0"))
                     .Append("</td><td>").Append(m is null ? "-" : Units.Bytes(r.OutputBytes)).Append("</td><td>").Append(m is null ? "-" : r.PageCount.ToString(Invariant))
                     .Append("</td><td>").Append(Units.Ratio(r.TimeRatio)).Append("</td><td>").Append(Units.Ratio(r.AllocationRatio)).Append("</td></tr>");
                }
                h.Append("</tbody></table></div>");
            }
        }
    }

    /// <summary>Horizontal bars from a zero baseline, library name on the left, value at the bar end, tooltip via title.</summary>
    private static void BarChart(StringBuilder h, string title, IReadOnlyList<(string Library, double Value, string Label)> bars)
    {
        const int labelWidth = 120, barHeight = 18, gap = 8, valueWidth = 90, width = 560;
        var plot = width - labelWidth - valueWidth;
        var max = bars.Max(b => b.Value);
        var height = bars.Count * (barHeight + gap) + 8;
        h.Append("<figure><svg viewBox=\"0 0 ").Append(width).Append(' ').Append(height).Append("\" width=\"100%\" role=\"img\" aria-label=\"")
         .Append(E(title)).Append(": ").Append(E(string.Join(", ", bars.Select(b => $"{Name(b.Library)} {b.Label}")))).Append("\">");
        for (var i = 0; i < bars.Count; i++)
        {
            var (library, value, label) = bars[i];
            var y = 4 + i * (barHeight + gap);
            var w = max > 0 ? Math.Max(2, value / max * plot) : 2;
            h.Append(CultureInfo.InvariantCulture, $"<text x=\"{labelWidth - 8}\" y=\"{y + 13}\" text-anchor=\"end\">{E(Name(library))}</text>");
            h.Append(CultureInfo.InvariantCulture, $"<path d=\"{BarPath(labelWidth, y, w, barHeight)}\" fill=\"var(--s{Slot.GetValueOrDefault(library, 1)})\"><title>{E(Name(library))}: {E(label)}</title></path>");
            h.Append(CultureInfo.InvariantCulture, $"<text class=\"v\" x=\"{labelWidth + w + 6:0.#}\" y=\"{y + 13}\">{E(label)}</text>");
        }
        h.Append("</svg><figcaption>").Append(E(title)).Append(" (lower is better)</figcaption></figure>");
    }

    /// <summary>A bar anchored square on the baseline with a 4 px rounded data end.</summary>
    private static string BarPath(double x, double y, double w, double hgt)
    {
        var r = Math.Min(4, w / 2);
        return string.Create(CultureInfo.InvariantCulture,
            $"M{x:0.#},{y:0.#}H{x + w - r:0.#}Q{x + w:0.#},{y:0.#} {x + w:0.#},{y + r:0.#}V{y + hgt - r:0.#}Q{x + w:0.#},{y + hgt:0.#} {x + w - r:0.#},{y + hgt:0.#}H{x:0.#}Z");
    }

    private static void Profiler(StringBuilder h, RunData data)
    {
        h.Append("<h2 id=\"profiler\">Process-level profile</h2>");
        if (data.Profiler.Count == 0)
        {
            h.Append("<p>The resource profiler was not run for this run folder.</p>");
            return;
        }
        h.Append("<p>Each measurement runs in a fresh child process (UnlimitedPDF.Benchmarks.ResourceProfiler). Latency percentiles are per document. ")
         .Append("Working set and private bytes are operating-system values for the whole process, including native memory and the .NET runtime; ")
         .Append("<i>retained heap</i> is the managed heap after the workload and two full compacting collections. These values are never added together.</p>");
        foreach (var workload in data.Profiler.Select(p => S(p, "workload")).Distinct())
        {
            var items = data.Profiler.Where(p => S(p, "workload") == workload).ToArray();
            h.Append("<h3>Workload <code>").Append(E(workload)).Append("</code></h3>");
            var cold = items.Where(p => S(p, "suite") == "ColdStart").GroupBy(p => S(p, "library")).ToArray();
            if (cold.Length > 0)
            {
                h.Append("<h4>Cold start (fresh process each run; medians)</h4><div class=\"scroll\"><table><thead><tr><th>Library</th><th class=\"l\">Status</th><th>Runs</th>")
                 .Append("<th>Process start → first PDF</th><th>Library init</th><th>First document</th><th>Parent-observed process time</th><th>Peak working set</th></tr></thead><tbody>");
                foreach (var g in cold)
                {
                    var passed = g.Where(p => S(p, "status") == "Passed").ToArray();
                    var status = passed.Length == g.Count() ? "Passed" : S(g.First(p => S(p, "status") != "Passed"), "status");
                    h.Append("<tr><td>").Append(Swatch(g.Key)).Append(E(Name(g.Key))).Append("</td><td class=\"l st-").Append(status).Append("\">").Append(E(status))
                     .Append(passed.Length == g.Count() ? "" : "<br><span class=\"note\">" + E(S(g.First(), "reason")) + "</span>")
                     .Append("</td><td>").Append(g.Count()).Append("</td><td>").Append(Units.Milliseconds(Median(passed, "processStartToFirstPdfMs")))
                     .Append("</td><td>").Append(Units.Milliseconds(Median(passed, "libraryInitializationMs"))).Append("</td><td>")
                     .Append(Units.Milliseconds(Median(passed, "firstDocumentMs"))).Append("</td><td>").Append(Units.Milliseconds(Median(passed, "parentObservedProcessMs")))
                     .Append("</td><td>").Append(Units.Bytes(Median(passed, "peakWorkingSetBytes"))).Append("</td></tr>");
                }
                h.Append("</tbody></table></div>");
            }

            var concurrent = items.Where(p => S(p, "suite") == "Concurrent" && S(p, "status") == "Passed").ToArray();
            if (concurrent.Select(p => I(p, "workers")).Distinct().Count() > 1)
                LineChart(h, $"Throughput by worker count, {workload} (higher is better)", "workers", "documents/s",
                    concurrent.GroupBy(p => S(p, "library")).Select(g => (g.Key, g.Select(p => ((double)I(p, "workers"), D(p, "documentsPerSecond"))).OrderBy(x => x.Item1).ToArray())).ToArray(),
                    v => Units.Number(v, "0"));

            var repeated = items.Where(p => S(p, "suite") != "ColdStart").ToArray();
            if (repeated.Length > 0)
            {
                h.Append("<h4>Throughput, latency and process resources</h4><div class=\"scroll\"><table><thead><tr><th>Suite</th><th>Library</th><th>Docs</th><th>Workers</th><th class=\"l\">Status</th>")
                 .Append("<th>Docs/s</th><th>P50</th><th>P95</th><th>P99</th><th>Max</th><th>CPU %</th><th>Alloc/doc</th><th>Gen0/1/2</th><th>Peak working set</th><th>Peak private</th>")
                 .Append("<th>Peak managed heap</th><th>Retained heap</th><th>Threads</th><th>Handles</th></tr></thead><tbody>");
                foreach (var p in repeated)
                {
                    var ok = S(p, "status") == "Passed";
                    JsonElement? l = p.TryGetProperty("latency", out var lat) && lat.ValueKind == JsonValueKind.Object ? lat : null;
                    string Lat(string n) => ok && l is { } x ? Units.Milliseconds(x.GetProperty(n).GetDouble()) : "-";
                    h.Append("<tr><td>").Append(E(S(p, "suite"))).Append("</td><td>").Append(Swatch(S(p, "library"))).Append(E(Name(S(p, "library")))).Append("</td><td>")
                     .Append(I(p, "documents")).Append("</td><td>").Append(I(p, "workers")).Append("</td><td class=\"l st-").Append(S(p, "status")).Append("\">")
                     .Append(E(S(p, "status"))).Append(ok ? "" : "<br><span class=\"note\">" + E(S(p, "reason")) + "</span>").Append("</td><td>")
                     .Append(ok ? Units.Number(D(p, "documentsPerSecond"), "0.0") : "-").Append("</td><td>").Append(Lat("p50Ms")).Append("</td><td>").Append(Lat("p95Ms"))
                     .Append("</td><td>").Append(Lat("p99Ms")).Append("</td><td>").Append(Lat("maxMs")).Append("</td><td>")
                     .Append(ok ? Units.Number(D(p, "cpuUtilizationPercent"), "0") : "-").Append("</td><td>").Append(ok ? Units.Bytes(D(p, "managedAllocatedBytesPerDocument")) : "-")
                     .Append("</td><td>").Append(ok ? $"{I(p, "gen0Collections")}/{I(p, "gen1Collections")}/{I(p, "gen2Collections")}" : "-").Append("</td><td>")
                     .Append(ok ? Units.Bytes(D(p, "peakWorkingSetBytes")) : "-").Append("</td><td>").Append(ok ? Units.Bytes(D(p, "peakPrivateBytes")) : "-").Append("</td><td>")
                     .Append(ok ? Units.Bytes(D(p, "peakManagedHeapBytes")) : "-").Append("</td><td>").Append(ok ? Units.Bytes(D(p, "retainedManagedHeapBytes")) : "-").Append("</td><td>")
                     .Append(ok ? I(p, "peakThreads").ToString(Invariant) : "-").Append("</td><td>").Append(ok ? I(p, "peakHandles").ToString(Invariant) : "-").Append("</td></tr>");
                }
                h.Append("</tbody></table></div>");
            }

            var longRunning = items.Where(p => S(p, "suite") == "LongRunning" && S(p, "status") == "Passed").ToArray();
            if (longRunning.Length > 0)
            {
                var largest = longRunning.Max(p => I(p, "documents"));
                var series = longRunning.Where(p => I(p, "documents") == largest).Select(p => (S(p, "library"), Timeline(p, "workingSetBytes"))).ToArray();
                h.Append("<div class=\"grid\">");
                LineChart(h, $"Working set over {largest:N0} documents, {workload}", "documents completed", "working set", series, v => Units.Bytes(v));
                LineChart(h, $"Managed heap in use over {largest:N0} documents, {workload}", "documents completed", "managed heap",
                    longRunning.Where(p => I(p, "documents") == largest).Select(p => (S(p, "library"), Timeline(p, "managedHeapBytes"))).ToArray(), v => Units.Bytes(v));
                h.Append("</div><p class=\"note\">Growth in these lines is an observation, not a confirmed leak; confirming a leak needs profiling evidence.</p>");
            }
        }
    }

    private static (double X, double Y)[] Timeline(JsonElement result, string field) =>
        result.TryGetProperty("timeline", out var t) && t.ValueKind == JsonValueKind.Array
            ? t.EnumerateArray().Select(s => ((double)s.GetProperty("documentsCompleted").GetInt64(), s.GetProperty(field).GetDouble())).ToArray()
            : [];

    /// <summary>Multi-series line chart, one y-axis, legend plus end-of-line labels.</summary>
    private static void LineChart(StringBuilder h, string title, string xLabel, string yLabel,
        IReadOnlyList<(string Library, (double X, double Y)[] Points)> series, Func<double, string> format)
    {
        series = series.Where(s => s.Points.Length > 0).ToArray();
        if (series.Count == 0)
            return;
        const int width = 560, height = 260, left = 70, right = 110, top = 12, bottom = 36;
        var xs = series.SelectMany(s => s.Points.Select(p => p.X)).ToArray();
        var ys = series.SelectMany(s => s.Points.Select(p => p.Y)).ToArray();
        double minX = xs.Min(), maxX = xs.Max(), maxY = ys.Max() * 1.08;
        if (maxX <= minX)
            maxX = minX + 1;
        if (maxY <= 0)
            maxY = 1;
        double X(double v) => left + (v - minX) / (maxX - minX) * (width - left - right);
        double Y(double v) => top + (1 - v / maxY) * (height - top - bottom);
        h.Append("<figure><svg viewBox=\"0 0 ").Append(width).Append(' ').Append(height).Append("\" width=\"100%\" role=\"img\" aria-label=\"").Append(E(title)).Append("\">");
        for (var i = 0; i <= 4; i++)
        {
            var v = maxY * i / 4;
            h.Append(CultureInfo.InvariantCulture, $"<line x1=\"{left}\" x2=\"{width - right}\" y1=\"{Y(v):0.#}\" y2=\"{Y(v):0.#}\" stroke=\"var(--border)\" stroke-width=\"1\"/>");
            h.Append(CultureInfo.InvariantCulture, $"<text x=\"{left - 6}\" y=\"{Y(v) + 4:0.#}\" text-anchor=\"end\">{E(format(v))}</text>");
        }
        foreach (var x in xs.Distinct().OrderBy(v => v).Take(12))
            h.Append(CultureInfo.InvariantCulture, $"<text x=\"{X(x):0.#}\" y=\"{height - bottom + 16}\" text-anchor=\"middle\">{Units.Number(x, "0")}</text>");
        h.Append(CultureInfo.InvariantCulture, $"<text x=\"{(left + width - right) / 2}\" y=\"{height - 4}\" text-anchor=\"middle\">{E(xLabel)}</text>");
        foreach (var (library, points) in series)
        {
            var color = $"var(--s{Slot.GetValueOrDefault(library, 1)})";
            var path = string.Join(' ', points.Select((p, i) => string.Create(Invariant, $"{(i == 0 ? 'M' : 'L')}{X(p.X):0.#},{Y(p.Y):0.#}")));
            h.Append("<path d=\"").Append(path).Append("\" fill=\"none\" stroke=\"").Append(color).Append("\" stroke-width=\"2\" stroke-linejoin=\"round\"/>");
            foreach (var p in points.Length <= 12 ? points : [])
                h.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{X(p.X):0.#}\" cy=\"{Y(p.Y):0.#}\" r=\"4\" fill=\"{color}\" stroke=\"var(--surface)\" stroke-width=\"2\"><title>{E(Name(library))}: {E(format(p.Y))} at {Units.Number(p.X, "0")}</title></circle>");
            var last = points[^1];
            h.Append(CultureInfo.InvariantCulture, $"<text x=\"{X(last.X) + 8:0.#}\" y=\"{Y(last.Y) + 4:0.#}\">{E(Name(library))}</text>");
        }
        h.Append("</svg><figcaption>").Append(E(title)).Append(". Legend: ");
        h.AppendJoin(", ", series.Select(s => Swatch(s.Library) + E(Name(s.Library))));
        h.Append(". Y axis: ").Append(E(yLabel)).Append(".</figcaption></figure>");
    }

    private static void Samples(StringBuilder h, RunData data)
    {
        h.Append("<h2 id=\"samples\">Output samples</h2><p>Page 1 of the smallest case of each scenario and library, rendered by PDFium. ")
         .Append("The matching PDFs are kept in <code>samples/</code> of the run folder for manual inspection.</p>");
        foreach (var type in data.Rows.Where(r => r.SampleImage is not null).GroupBy(r => r.Scenario))
        {
            h.Append("<details><summary>").Append(E(type.Key)).Append("</summary><div class=\"thumbs\">");
            foreach (var r in type)
            {
                var path = r.SampleImage!;
                if (!File.Exists(path))
                    continue;
                h.Append("<figure><img loading=\"lazy\" alt=\"Page 1 of ").Append(E(Name(r.Library))).Append(" output for ").Append(E(r.Scenario)).Append(", ").Append(E(r.Parameters))
                 .Append("\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append("\"><figcaption>")
                 .Append(E(Name(r.Library))).Append(" · ").Append(E(r.Parameters)).Append(" · ").Append(StatusText(r.Status)).Append("</figcaption></figure>");
            }
            h.Append("</div></details>");
        }
    }

    private static void Licensing(StringBuilder h)
    {
        h.Append("<h2 id=\"licensing\">Licensing summary</h2><p class=\"note\">Verified ").Append(LibraryCatalog.VerifiedOn)
         .Append(" for the exact versions used. Technical due diligence, not legal advice; see docs/library-licenses.md.</p><div class=\"scroll\"><table><thead><tr>")
         .Append("<th>Library</th><th>Package</th><th>Version used</th><th>Latest stable</th><th>Released</th><th>SPDX</th><th class=\"l\">Notes</th></tr></thead><tbody>");
        foreach (var l in LibraryCatalog.All)
            h.Append("<tr><td>").Append(Swatch(l.Id)).Append(E(l.DisplayName)).Append("</td><td>").Append(E(l.PackageId)).Append("</td><td>").Append(E(l.Version))
             .Append("</td><td>").Append(E(l.LatestStableVersion)).Append("</td><td>").Append(E(l.ReleaseDate)).Append("</td><td><a href=\"").Append(E(l.LicenseUrl))
             .Append("\">").Append(E(l.Spdx)).Append("</a></td><td class=\"l\">").Append(E(l.Notes)).Append("</td></tr>");
        h.Append("</tbody></table></div><h3>Excluded candidates</h3><ul>");
        foreach (var (package, reason) in LibraryCatalog.Excluded)
            h.Append("<li><b>").Append(E(package)).Append("</b>: ").Append(E(reason)).Append("</li>");
        h.Append("</ul>");
    }

    private static void Methodology(StringBuilder h) =>
        h.Append("<h2 id=\"methodology\">Methodology in brief</h2><ul>")
         .Append("<li>BenchmarkDotNet measures in-memory generation including full serialization to a byte array (file output is a separate category). Fixtures, adapters and fonts are prepared outside measured methods.</li>")
         .Append("<li>Every library receives identical inputs: the same Noto font files, image bytes, text, table data, HTML and manipulation input PDFs (written by an independent fixture writer).</li>")
         .Append("<li>Each library uses its documented public API and defaults, with reusable objects (fonts, typefaces) created once where the API offers them. Known differences are listed per scenario and in docs/methodology.md.</li>")
         .Append("<li>BenchmarkDotNet iteration statistics are not request latency; per-document percentiles come from the resource profiler.</li>")
         .Append("<li>Results from one machine are not comparable with another machine without qualification. Shared CI runners are informational only.</li></ul>");

    private static bool IsWildcard(ResultRow r) => r.CaseId.EndsWith("(*)", StringComparison.Ordinal);

    private static string Swatch(string library) =>
        $"<span class=\"sw\" style=\"background:var(--s{Slot.GetValueOrDefault(library, 1)})\" aria-hidden=\"true\"></span>";

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);
}
