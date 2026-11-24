using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Profiles;
using UnlimitedPDF.Benchmarks.Core.Provenance;

namespace UnlimitedPDF.Benchmarks.ResourceProfiler;

public static class Program
{
    private const string Usage = """
        UnlimitedPDF.Benchmarks.ResourceProfiler [options]

          --profile smoke|quick|standard|full|stress   Workload sizes (default: standard).
          --output <folder>                            Run folder; results go to <folder>/profiler (default: results/<run-id>).
          --library <ids>                              Comma-separated library ids.
          --suite <names>                              ColdStart,Sequential,Concurrent,LongRunning (default: all).
          --timeout-minutes <n>                        Per worker process (default: 30).

        Every measurement runs in a fresh child process (the 'worker' command), one library at a time.
        """;

    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "worker")
            return RunWorker(args);
        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine(Usage);
            return 0;
        }

        using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        }));
        var logger = loggerFactory.CreateLogger("Profiler");
        var options = Options.Parse(args);
        var profile = BenchmarkProfile.Parse(options.Get("--profile") ?? "standard");
        var settings = profile.Profiler;
        var root = RunManifest.FindRepositoryRoot();
        var started = DateTimeOffset.UtcNow;
        var runFolder = Path.GetFullPath(options.Get("--output") ?? Path.Combine(root, "results", RunManifest.NewRunId(started)));
        var output = Path.Combine(runFolder, "profiler");
        Directory.CreateDirectory(output);
        var libraries = options.Get("--library")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        var suites = (options.Get("--suite")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      ?? Enum.GetNames<Suite>()).Select(Enum.Parse<Suite>).ToHashSet();
        var timeout = TimeSpan.FromMinutes(int.Parse(options.Get("--timeout-minutes") ?? "30", CultureInfo.InvariantCulture));

        if (!File.Exists(Path.Combine(runFolder, "manifest.json")))
            new RunManifest(Path.GetFileName(runFolder), started, profile.Name.ToString(), args, GitInfo.Capture(root),
                EnvironmentInfo.Capture(Assembly.GetExecutingAssembly()),
                [ComponentVersions.Of("ResourceProfiler", Assembly.GetExecutingAssembly())], FixtureManifest.Version,
                FixtureManifest.Sha256(), FixtureManifest.FontChecksums()).Save(Path.Combine(runFolder, "manifest.json"));

        var results = new List<WorkerResult>();
        void Record(WorkerResult result)
        {
            results.Add(result);
            logger.LogInformation("{Suite,-11} {Library,-12} {Workload,-17} docs={Docs,-6} workers={Workers,-2} {Status} {Detail}",
                result.Suite, result.Library, result.Workload, result.Documents, result.Workers, result.Status,
                result.Status == ResultStatus.Passed
                    ? result.Suite == Suite.ColdStart
                        ? $"first PDF {result.ProcessStartToFirstPdfMs:0} ms after process start"
                        : $"{result.DocumentsPerSecond:0.0} docs/s, p95 {result.Latency?.P95Ms:0.000} ms"
                    : result.Reason);
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, RunManifest.JsonOptions));
        }

        foreach (var workload in Workload.All)
        {
            var participants = workload.Libraries.Where(l => libraries is null || libraries.Contains(l)).ToArray();
            var runnable = participants;

            if (suites.Contains(Suite.ColdStart))
                for (var run = 0; run < settings.ColdStartRuns; run++)
                    foreach (var library in runnable) // round-robin so drift affects every library alike
                        Record(Spawn(Suite.ColdStart, library, workload, 1, 1, settings.SampleIntervalMs, timeout));
            if (suites.Contains(Suite.Sequential))
                foreach (var count in settings.SequentialCounts)
                    foreach (var library in runnable)
                        Record(Spawn(Suite.Sequential, library, workload, count, 1, settings.SampleIntervalMs, timeout));
            if (suites.Contains(Suite.Concurrent))
                foreach (var workers in settings.ConcurrencyLevels)
                    foreach (var library in runnable)
                        Record(Spawn(Suite.Concurrent, library, workload, settings.ConcurrentDocuments, workers, settings.SampleIntervalMs, timeout));
            // Long-running memory is tracked for the two document workloads; HTML conversion is covered by the other suites.
            if (suites.Contains(Suite.LongRunning) && workload.Id is Workload.Text or Workload.Invoice)
                foreach (var count in settings.LongRunningDocuments)
                    foreach (var library in runnable)
                        Record(Spawn(Suite.LongRunning, library, workload, count, 1, settings.SampleIntervalMs, timeout));
        }
        logger.LogInformation("Profiler finished: {Count} results in {Path}", results.Count, Path.Combine(output, "results.json"));
        return 0;
    }

    private static WorkerResult Skipped(Suite suite, string library, Workload workload, string reason) =>
        new() { Suite = suite, Library = library, Workload = workload.Id, Status = ResultStatus.NotExecuted, Reason = reason };

    private static WorkerResult Spawn(Suite suite, string library, Workload workload, int documents, int workers, int intervalMs, TimeSpan timeout)
    {
        var resultFile = Path.Combine(Path.GetTempPath(), $"updf-profiler-{Guid.NewGuid():N}.json");
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in new[] { "worker", suite.ToString(), library, workload.Id, documents.ToString(CultureInfo.InvariantCulture),
                     workers.ToString(CultureInfo.InvariantCulture), intervalMs.ToString(CultureInfo.InvariantCulture), resultFile })
            start.ArgumentList.Add(arg);
        var clock = Stopwatch.StartNew();
        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync();
        _ = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            return Skipped(suite, library, workload, $"Worker did not finish within {timeout.TotalMinutes:0} minutes.") with
            {
                Status = ResultStatus.TimedOut,
                Documents = documents,
                Workers = workers,
            };
        }
        var observed = clock.Elapsed.TotalMilliseconds;
        try
        {
            if (process.ExitCode != 0 || !File.Exists(resultFile))
                return Skipped(suite, library, workload, $"Worker exited with code {process.ExitCode}: {Truncate(stderr.Result)}") with
                {
                    Status = ResultStatus.ExecutionFailed,
                    Documents = documents,
                    Workers = workers,
                };
            var result = JsonSerializer.Deserialize<WorkerResult>(File.ReadAllText(resultFile), RunManifest.JsonOptions)!;
            return result with { ParentObservedProcessMs = observed };
        }
        finally
        {
            File.Delete(resultFile);
        }
    }

    private static int RunWorker(string[] args)
    {
        var suite = Enum.Parse<Suite>(args[1]);
        var library = args[2];
        var workload = Workload.Get(args[3]);
        var documents = int.Parse(args[4], CultureInfo.InvariantCulture);
        var workers = int.Parse(args[5], CultureInfo.InvariantCulture);
        var interval = int.Parse(args[6], CultureInfo.InvariantCulture);
        var result = Worker.Run(suite, library, workload, documents, workers, interval);
        File.WriteAllText(args[7], JsonSerializer.Serialize(result, RunManifest.JsonOptions));
        return 0;
    }

    private static string Truncate(string text) => text.Length <= 400 ? text.Trim() : text[..400].Trim() + "...";

    private sealed class Options(Dictionary<string, string> values)
    {
        public string? Get(string name) => values.TryGetValue(name, out var value) ? value : null;

        public static Options Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i].StartsWith("--", StringComparison.Ordinal))
                    values[args[i]] = args[++i];
            return new Options(values);
        }
    }
}
