using System.Diagnostics;

namespace UnlimitedPDF.Benchmarks.ResourceProfiler;

/// <summary>One point in time. Byte values are exactly what the OS or runtime reports; they are never combined.</summary>
public sealed record ResourceSample(
    double ElapsedMs,
    long DocumentsCompleted,
    long WorkingSetBytes,
    long PrivateBytes,
    long ManagedHeapBytes,
    long GcHeapSizeBytes,
    int Threads,
    int Handles);

/// <summary>
/// Samples the current process on a background thread at a fixed interval. Overhead: one Process.Refresh per sample.
/// </summary>
public sealed class ProcessSampler : IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Stopwatch _clock;
    private readonly Func<long> _completed;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _stop = new();
    private readonly List<ResourceSample> _samples = [];
    private readonly int _intervalMs;

    public ProcessSampler(int intervalMs, Stopwatch clock, Func<long> completed)
    {
        _intervalMs = intervalMs;
        _clock = clock;
        _completed = completed;
        _thread = new Thread(Run) { IsBackground = true, Name = "resource-sampler", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    public IReadOnlyList<ResourceSample> Samples
    {
        get
        {
            lock (_samples)
                return _samples.ToArray();
        }
    }

    public ResourceSample SampleNow()
    {
        _process.Refresh();
        return new ResourceSample(_clock.Elapsed.TotalMilliseconds, _completed(), _process.WorkingSet64, _process.PrivateMemorySize64,
            GC.GetTotalMemory(false), GC.GetGCMemoryInfo().HeapSizeBytes, _process.Threads.Count, SafeHandles());
    }

    private int SafeHandles()
    {
        try
        {
            return _process.HandleCount;
        }
        catch (PlatformNotSupportedException)
        {
            return -1;
        }
    }

    private void Run()
    {
        do
        {
            var sample = SampleNow();
            lock (_samples)
                _samples.Add(sample);
        }
        while (!_stop.Wait(_intervalMs));
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _stop.Set();
        _thread.Join();
        var last = SampleNow();
        lock (_samples)
            _samples.Add(last);
        _stop.Dispose();
    }
}
