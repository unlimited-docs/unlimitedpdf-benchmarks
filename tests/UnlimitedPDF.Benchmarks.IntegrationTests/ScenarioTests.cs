using System.Runtime.CompilerServices;
using System.Text.Json;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Profiles;
using UnlimitedPDF.Benchmarks.Core.Provenance;
using UnlimitedPDF.Benchmarks.Reporting;
using UnlimitedPDF.Benchmarks.ResourceProfiler;
using UnlimitedPDF.Benchmarks.Runner.Infrastructure;

namespace UnlimitedPDF.Benchmarks.IntegrationTests;

internal static class SmokeProfile
{
    [ModuleInitializer]
    internal static void Select() => Environment.SetEnvironmentVariable(BenchmarkProfile.EnvironmentVariable, "smoke");
}

/// <summary>
/// Runs every scenario, library and smoke-size parameter combination through the same code path as the validation pass and
/// requires independent validation to pass, or the case to be a documented unsupported combination.
/// </summary>
public class ScenarioTests
{
    public static TheoryData<string> CaseIds()
    {
        var data = new TheoryData<string>();
        foreach (var type in ValidationPass.BenchmarkTypes())
            foreach (var c in ValidationPass.Cases(type))
                data.Add(c.CaseId);
        return data;
    }

    private static readonly Lazy<Dictionary<string, BenchmarkCaseDefinition>> Definitions = new(() =>
        ValidationPass.BenchmarkTypes().SelectMany(ValidationPass.Cases).ToDictionary(c => c.CaseId));

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void CaseProducesIndependentlyValidOutput(string caseId)
    {
        var definition = Definitions.Value[caseId];
        var record = ValidationPass.RunCase(definition, ValidationPass.ScenarioOf(definition.Type), TimeSpan.FromMinutes(5), null);
        if (record.Status == ResultStatus.Unsupported)
        {
            Assert.False(string.IsNullOrWhiteSpace(record.Reason));
            return;
        }
        Assert.True(record.Status == ResultStatus.Passed, $"{record.Status}: {record.Reason} {string.Join("; ", record.Failures)}");
        Assert.True(record.OutputBytes > 0);
        Assert.Contains(PdfValidatorName, record.Validators);
    }

    private const string PdfValidatorName = UnlimitedPDF.Benchmarks.Core.Validation.PdfValidator.PdfiumValidator;

    [Fact]
    public void EveryLibraryParticipatesInAtLeastOneScenario()
    {
        var libraries = ValidationPass.BenchmarkTypes().SelectMany(ValidationPass.BenchmarkMethods).Select(m => m.Name).ToHashSet();
        Assert.Equal(LibraryIds.All.ToHashSet(), libraries);
    }

    [Fact]
    public void EveryScenarioDeclaresEachLibraryAsMeasuredOrNotApplicable()
    {
        foreach (var type in ValidationPass.BenchmarkTypes())
        {
            var measured = ValidationPass.BenchmarkMethods(type).Select(m => m.Name);
            var notApplicable = ValidationPass.NotApplicable(type).Select(n => n.Library);
            Assert.Equal(LibraryIds.All.Order(), measured.Concat(notApplicable).Order());
        }
    }

    [Fact]
    public void BaselineIsUnlimitedPdfWhereItParticipates()
    {
        foreach (var type in ValidationPass.BenchmarkTypes())
        {
            var methods = ValidationPass.BenchmarkMethods(type).ToArray();
            var baseline = methods.Where(m => m.GetCustomAttributes(typeof(BenchmarkDotNet.Attributes.BenchmarkAttribute), false)
                .Cast<BenchmarkDotNet.Attributes.BenchmarkAttribute>().Single().Baseline).Select(m => m.Name).ToArray();
            Assert.Equal(methods.Any(m => m.Name == LibraryIds.UnlimitedPdf) ? [LibraryIds.UnlimitedPdf] : Array.Empty<string>(), baseline);
        }
    }

    [Fact]
    public void CaseKeysMatchTheReportingParser()
    {
        var definition = ValidationPass.Cases(typeof(UnlimitedPDF.Benchmarks.Runner.Benchmarks.ImageBenchmarks)).First();
        var parsed = RunData.ParseParameters(string.Join("&", definition.ParameterStrings.Select(p => $"{p.Key}={p.Value}")));
        Assert.Equal(definition.CaseId, RunData.CaseKey(definition.Type.Name, definition.Library, parsed));
    }
}

public class ProfilerAndReportTests
{
    [Theory]
    [InlineData(Suite.Sequential, LibraryIds.UnlimitedPdf, Workload.Text, 1)]
    [InlineData(Suite.Concurrent, LibraryIds.PdfSharp, Workload.Text, 2)]
    [InlineData(Suite.LongRunning, LibraryIds.MigraDoc, Workload.Invoice, 1)]
    [InlineData(Suite.ColdStart, LibraryIds.SkiaSharp, Workload.Text, 1)]
    public void WorkerMeasuresAndValidates(Suite suite, string library, string workload, int workers)
    {
        var result = Worker.Run(suite, library, Workload.Get(workload), documents: 6, workers, intervalMs: 20);
        Assert.Equal(ResultStatus.Passed, result.Status);
        Assert.True(result.OutputBytes > 0);
        if (suite != Suite.ColdStart)
        {
            Assert.Equal(6, result.Succeeded);
            Assert.NotNull(result.Latency);
            Assert.True(result.Latency!.P95Ms >= result.Latency.P50Ms);
            Assert.True(result.PeakWorkingSetBytes > 0);
            Assert.NotEmpty(result.Timeline);
        }
        else
        {
            Assert.True(result.ProcessStartToFirstPdfMs > 0);
        }
    }

    [Fact]
    public void ReportComputesRatiosOnlyAgainstAPassedBaseline()
    {
        var folder = Directory.CreateTempSubdirectory("updf-report-").FullName;
        try
        {
            CaseRecord Record(string library, ResultStatus status) => new($"T.{library}(Pages=1)", "T", "Scenario", "Generation", library,
                new() { ["Pages"] = "1" }, status, null, [], 1, 1000, 1, 1, [], [], null, null);
            File.WriteAllText(Path.Combine(folder, "validation.json"), JsonSerializer.Serialize(new[]
            {
                Record(LibraryIds.UnlimitedPdf, ResultStatus.Passed), Record(LibraryIds.PdfSharp, ResultStatus.Passed),
                Record(LibraryIds.PdfPig, ResultStatus.FailedValidation),
            }, RunManifest.JsonOptions));
            Directory.CreateDirectory(Path.Combine(folder, "bdn", "results"));
            object Bench(string method, double mean, long allocated) => new
            {
                Type = "T", Method = method, Parameters = "Pages=1", DisplayInfo = "T.M: DefaultJob [Pages=1]",
                Statistics = new { N = 10, Mean = mean, Median = mean, Min = mean, Max = mean, StandardDeviation = 1.0, StandardError = 0.5 },
                Memory = new { Gen0Collections = 0, Gen1Collections = 0, Gen2Collections = 0, TotalOperations = 1000, BytesAllocatedPerOperation = allocated },
            };
            File.WriteAllText(Path.Combine(folder, "bdn", "results", "T-report-full.json"), JsonSerializer.Serialize(new
            {
                Benchmarks = new[] { Bench("UnlimitedPdf", 100, 1000), Bench("PdfSharp", 250, 500), Bench("PdfPig", 50, 10) },
            }));

            var data = RunData.Load(folder);
            var sharp = data.Rows.Single(r => r.Library == LibraryIds.PdfSharp);
            Assert.Equal(2.5, sharp.TimeRatio);
            Assert.Equal(0.5, sharp.AllocationRatio);
            var pig = data.Rows.Single(r => r.Library == LibraryIds.PdfPig);
            Assert.Null(pig.TimeRatio);
            Assert.Equal(ResultStatus.FailedValidation, pig.Status);

            HtmlReport.Write(data, Path.Combine(folder, "report.html"));
            DataWriters.WriteMarkdown(data, Path.Combine(folder, "summary.md"));
            DataWriters.WriteCsv(data, Path.Combine(folder, "results.csv"));
            Assert.Contains("2.50×", File.ReadAllText(Path.Combine(folder, "summary.md")), StringComparison.Ordinal);
            Assert.DoesNotContain("| 50.0 ns |", File.ReadAllText(Path.Combine(folder, "summary.md")), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
