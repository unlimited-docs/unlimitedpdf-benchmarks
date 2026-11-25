using System.Diagnostics;
using System.Runtime;
using UnlimitedPDF.Benchmarks.Core;

namespace UnlimitedPDF.Benchmarks.ResourceProfiler;

public enum Suite
{
    ColdStart,
    Sequential,
    Concurrent,
    LongRunning,
}

public sealed record LatencySummary(int Count, double MeanMs, double P50Ms, double P95Ms, double P99Ms, double MaxMs, double MinMs)
{
    public static LatencySummary? From(List<double> values)
    {
        if (values.Count == 0)
            return null;
        values.Sort();
        return new LatencySummary(values.Count, values.Average(), Statistics.Percentile(values, 50), Statistics.Percentile(values, 95),
            Statistics.Percentile(values, 99), values[^1], values[0]);
    }
}

/// <summary>Result of one isolated worker process. Every memory field states what it measures; values are never combined.</summary>
public sealed record WorkerResult
{
    public Suite Suite { get; init; }
    public string Library { get; init; } = "";
    public string Workload { get; init; } = "";
    public int Documents { get; init; }
    public int Workers { get; init; }
    public ResultStatus Status { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyList<string> ValidationFailures { get; init; } = [];

    /// <summary>Size of one output document in bytes.</summary>
    public long OutputBytes { get; init; }

    public double WallMs { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public double DocumentsPerSecond { get; init; }
    public double PagesPerSecond { get; init; }

    /// <summary>Per-document latency around each generate call (request level, unlike BenchmarkDotNet iteration statistics).</summary>
    public LatencySummary? Latency { get; init; }

    /// <summary>Process CPU time (user + kernel) during the measured window.</summary>
    public double ProcessCpuMs { get; init; }

    /// <summary>Process CPU time / (wall time x logical CPUs) x 100.</summary>
    public double CpuUtilizationPercent { get; init; }

    /// <summary>Cumulative managed allocations (GC.GetTotalAllocatedBytes); not retained, peak or native memory.</summary>
    public long ManagedAllocatedBytes { get; init; }

    public double ManagedAllocatedBytesPerDocument { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }

    /// <summary>OS peak working set of this process (includes native memory and runtime overhead).</summary>
    public long PeakWorkingSetBytes { get; init; }

    /// <summary>Highest sampled private bytes (committed memory not shared with other processes).</summary>
    public long PeakPrivateBytes { get; init; }

    /// <summary>Highest sampled GC.GetTotalMemory(false): managed heap in use, not allocations.</summary>
    public long PeakManagedHeapBytes { get; init; }

    public int PeakThreads { get; init; }
    public int PeakHandles { get; init; }

    /// <summary>Working set after warm-up and a full blocking GC, before the measured window.</summary>
    public long BaselineWorkingSetBytes { get; init; }

    public long BaselineManagedHeapBytes { get; init; }

    /// <summary>Managed heap after the workload and two full compacting GCs: live (retained) managed memory.</summary>
    public long RetainedManagedHeapBytes { get; init; }

    public long WorkingSetAfterFullGcBytes { get; init; }
    public double ProcessStartToMainMs { get; init; }
    public double LibraryInitializationMs { get; init; }
    public double FirstDocumentMs { get; init; }
    public double ProcessStartToFirstPdfMs { get; init; }

    /// <summary>Wall time the parent observed from Process.Start to exit (includes runtime start-up and shutdown).</summary>
    public double ParentObservedProcessMs { get; init; }

    public string GcMode { get; init; } = "";
    public int SampleIntervalMs { get; init; }
    public IReadOnlyList<ResourceSample> Timeline { get; init; } = [];
}

public static class Worker
{
    public static WorkerResult Run(Suite suite, string library, Workload workload, int documents, int workers, int intervalMs)
    {
        var process = Process.GetCurrentProcess();
        var template = new WorkerResult
        {
            Suite = suite,
            Library = library,
            Workload = workload.Id,
            Documents = documents,
            Workers = workers,
            GcMode = GCSettings.IsServerGC ? "Server" : "Workstation",
            SampleIntervalMs = intervalMs,
            ProcessStartToMainMs = (DateTime.Now - process.StartTime).TotalMilliseconds,
        };
        return suite == Suite.ColdStart ? ColdStart(template, process, library, workload) : Repeated(template, process, library, workload, documents, workers, intervalMs);
    }

    /// <summary>Library initialisation and the first document in a fresh process, nothing else.</summary>
    private static WorkerResult ColdStart(WorkerResult template, Process process, string library, Workload workload)
    {
        var initClock = Stopwatch.StartNew();
        var (adapter, generate) = workload.Create(library);
        var initMs = initClock.Elapsed.TotalMilliseconds;
        var firstClock = Stopwatch.StartNew();
        var pdf = generate();
        var firstMs = firstClock.Elapsed.TotalMilliseconds;
        var toFirst = (DateTime.Now - process.StartTime).TotalMilliseconds;
        process.Refresh();
        var result = template with
        {
            OutputBytes = pdf.LongLength,
            WallMs = initMs + firstMs,
            Succeeded = 1,
            LibraryInitializationMs = initMs,
            FirstDocumentMs = firstMs,
            ProcessStartToFirstPdfMs = toFirst,
            PeakWorkingSetBytes = process.PeakWorkingSet64,
            PeakPrivateBytes = process.PrivateMemorySize64,
            PeakManagedHeapBytes = GC.GetTotalMemory(false),
            PeakThreads = process.Threads.Count,
        };
        var validation = workload.Validate(library, pdf);
        (adapter as IDisposable)?.Dispose();
        return result with { Status = validation.Status, ValidationFailures = validation.Failures };
    }

    private static WorkerResult Repeated(WorkerResult template, Process process, string library, Workload workload, int documents, int workers, int intervalMs)
    {
        var instances = Enumerable.Range(0, workers).Select(_ => workload.Create(library)).ToArray();
        try
        {
            // Warm-up outside measurement; also validates the first output of every worker instance.
            var failures = new List<string>();
            long outputBytes = 0;
            foreach (var (_, generate) in instances)
            {
                var warm = generate();
                outputBytes = warm.LongLength;
                var report = workload.Validate(library, warm);
                if (!report.Passed)
                    failures.AddRange(report.Failures);
            }
            if (failures.Count > 0)
                return template with { Status = ResultStatus.FailedValidation, ValidationFailures = failures, OutputBytes = outputBytes };

            FullCollect();
            process.Refresh();
            var baselineWs = process.WorkingSet64;
            var baselineHeap = GC.GetTotalMemory(false);
            var cpuBefore = process.TotalProcessorTime;
            var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            var gc0 = GC.CollectionCount(0);
            var gc1 = GC.CollectionCount(1);
            var gc2 = GC.CollectionCount(2);

            long completed = 0;
            var failed = 0;
            var next = -1;
            byte[]? last = null;
            var latencies = new List<double>[workers];
            var clock = Stopwatch.StartNew();
            var sampler = new ProcessSampler(intervalMs, clock, () => Interlocked.Read(ref completed));
            var threads = new Thread[workers];
            for (var w = 0; w < workers; w++)
            {
                var index = w;
                latencies[index] = new List<double>(documents / workers + 1);
                threads[w] = new Thread(() =>
                {
                    var generate = instances[index].Generate;
                    while (Interlocked.Increment(ref next) < documents)
                    {
                        var start = Stopwatch.GetTimestamp();
                        try
                        {
                            var pdf = generate();
                            latencies[index].Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                            Interlocked.Increment(ref completed);
                            if (index == 0)
                                last = pdf;
                        }
                        catch (Exception)
                        {
                            Interlocked.Increment(ref failed);
                        }
                    }
                }) { IsBackground = true, Name = $"worker-{w}" };
                threads[w].Start();
            }
            foreach (var thread in threads)
                thread.Join();
            var wall = clock.Elapsed.TotalMilliseconds;
            sampler.Dispose();

            process.Refresh();
            var cpuMs = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
            var g0 = GC.CollectionCount(0) - gc0;
            var g1 = GC.CollectionCount(1) - gc1;
            var g2 = GC.CollectionCount(2) - gc2;
            var peakWorkingSet = process.PeakWorkingSet64;
            var samples = sampler.Samples;
            var lastValidation = last is null ? null : workload.Validate(library, last);
            FullCollect();
            process.Refresh();
            var succeeded = (int)Interlocked.Read(ref completed);
            var status = failed > 0 ? ResultStatus.ExecutionFailed
                : lastValidation is { Passed: false } ? ResultStatus.FailedValidation : ResultStatus.Passed;
            return template with
            {
                Status = status,
                Reason = failed > 0 ? $"{failed} of {documents} documents threw." : null,
                ValidationFailures = lastValidation?.Failures ?? [],
                OutputBytes = outputBytes,
                WallMs = wall,
                Succeeded = succeeded,
                Failed = failed,
                DocumentsPerSecond = succeeded / (wall / 1000),
                PagesPerSecond = succeeded * workload.PagesPerDocument / (wall / 1000),
                Latency = LatencySummary.From(latencies.SelectMany(l => l).ToList()),
                ProcessCpuMs = cpuMs,
                CpuUtilizationPercent = cpuMs / (wall * Environment.ProcessorCount) * 100,
                ManagedAllocatedBytes = allocated,
                ManagedAllocatedBytesPerDocument = succeeded > 0 ? (double)allocated / succeeded : 0,
                Gen0Collections = g0,
                Gen1Collections = g1,
                Gen2Collections = g2,
                PeakWorkingSetBytes = Math.Max(peakWorkingSet, samples.Max(s => s.WorkingSetBytes)),
                PeakPrivateBytes = samples.Max(s => s.PrivateBytes),
                PeakManagedHeapBytes = samples.Max(s => s.ManagedHeapBytes),
                PeakThreads = samples.Max(s => s.Threads),
                PeakHandles = samples.Max(s => s.Handles),
                BaselineWorkingSetBytes = baselineWs,
                BaselineManagedHeapBytes = baselineHeap,
                RetainedManagedHeapBytes = GC.GetTotalMemory(false),
                WorkingSetAfterFullGcBytes = process.WorkingSet64,
                Timeline = template.Suite == Suite.LongRunning ? samples : Thin(samples),
            };
        }
        finally
        {
            foreach (var (adapter, _) in instances)
                (adapter as IDisposable)?.Dispose();
        }
    }

    private static void FullCollect()
    {
        for (var i = 0; i < 2; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private static IReadOnlyList<ResourceSample> Thin(IReadOnlyList<ResourceSample> samples)
    {
        if (samples.Count <= 200)
            return samples;
        var step = samples.Count / 200.0;
        return Enumerable.Range(0, 200).Select(i => samples[(int)(i * step)]).Append(samples[^1]).ToArray();
    }
}
