using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Profiles;

namespace UnlimitedPDF.Benchmarks.Runner.Infrastructure;

/// <summary>Only cases that passed the validation pass are measured; failed, unsupported and unexecuted cases never get numbers.</summary>
public sealed class ValidatedCaseFilter(IReadOnlyList<ValidationRecord> records, IReadOnlySet<string>? libraries) : IFilter
{
    private readonly HashSet<string> _passed = records.Where(r => r.Status == ResultStatus.Passed).Select(r => r.CaseId).ToHashSet(StringComparer.Ordinal);

    public bool Predicate(BenchmarkCase benchmarkCase)
    {
        var method = benchmarkCase.Descriptor.WorkloadMethod.Name;
        if (libraries is not null && !libraries.Contains(method))
            return false;
        var id = CaseKey.Create(benchmarkCase.Descriptor.Type.Name, method,
            benchmarkCase.Parameters.Items.Select(p => (p.Name, p.Value ?? (object)string.Empty)));
        return _passed.Contains(id);
    }
}

public static class BenchmarkConfiguration
{
    public static IConfig Create(BenchmarkProfile profile, string artifactsPath, IReadOnlyList<ValidationRecord> records,
        IReadOnlySet<string>? libraries, bool jobFromCommandLine)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithArtifactsPath(artifactsPath)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(JsonExporter.Full, CsvExporter.Default, HtmlExporter.Default, MarkdownExporter.GitHub)
            .AddColumn(StatisticColumn.Median, StatisticColumn.Min, StatisticColumn.Max, StatisticColumn.StdErr, StatisticColumn.P95)
            .AddFilter(new ValidatedCaseFilter(records, libraries))
            .WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Value));
        if (!jobFromCommandLine)
            config = config.AddJob(JobFor(profile.Job));
        return config;
    }

    public static Job JobFor(MeasurementJob job) => job switch
    {
        MeasurementJob.Dry => Job.Dry,
        MeasurementJob.Short => Job.ShortRun,
        MeasurementJob.Monitoring => Job.Default.WithStrategy(RunStrategy.Monitoring).WithLaunchCount(1).WithWarmupCount(1)
            .WithIterationCount(5).WithId("Monitoring"),
        _ => Job.Default,
    };
}
