using System.Reflection;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Profiles;
using UnlimitedPDF.Benchmarks.Core.Provenance;
using UnlimitedPDF.Benchmarks.Runner.Infrastructure;

namespace UnlimitedPDF.Benchmarks.Runner;

public static class Program
{
    private const string Usage = """
        UnlimitedPDF.Benchmarks.Runner [run|validate|bench|list] [options] [BenchmarkDotNet options]

          run        Validate every case for the profile, then measure the passing cases (default).
          validate   Execute and independently validate each case once; writes manifest.json and validation.json.
          bench      Measure the cases that passed an earlier 'validate' into the same --output folder.
          list       Print every case of the profile and exit.

          --profile smoke|quick|standard|full|stress   Parameter sizes and default job (default: standard).
          --output <folder>                            Run folder (default: results/<run-id>).
          --library <ids>                              Comma-separated library ids, e.g. UnlimitedPdf,PdfPig.
          --category <names>                           Comma-separated: Generation, Layout, BusinessDocuments,
                                                       Multilingual, Manipulation, HtmlToPdf, FileOutput.
          --timeout-minutes <n>                        Validation timeout per case (default: 20).

        Any other option goes to BenchmarkDotNet, for example --filter '*TableBenchmarks*' or --job short.
        """;

    public static int Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        }));
        var logger = loggerFactory.CreateLogger("Runner");

        var remaining = new List<string>(args);
        var mode = remaining.Count > 0 && !remaining[0].StartsWith('-') ? Take(remaining, 0) : "run";
        if (remaining.Contains("--help") || remaining.Contains("-h") || mode == "help")
        {
            Console.WriteLine(Usage);
            return 0;
        }

        var profile = BenchmarkProfile.Parse(TakeOption(remaining, "--profile") ?? "standard");
        Environment.SetEnvironmentVariable(BenchmarkProfile.EnvironmentVariable, profile.Name.ToString());
        var libraries = TakeOption(remaining, "--library")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        if (libraries is not null && libraries.FirstOrDefault(l => !LibraryIds.All.Contains(l)) is { } unknown)
        {
            logger.LogError("Unknown library '{Library}'. Use: {All}", unknown, string.Join(", ", LibraryIds.All));
            return 2;
        }
        var categories = TakeOption(remaining, "--category")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownCategories = ValidationPass.BenchmarkTypes().Select(t => ValidationPass.ScenarioOf(t).Category).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (categories is not null && categories.FirstOrDefault(c => !knownCategories.Contains(c)) is { } unknownCategory)
        {
            logger.LogError("Unknown category '{Category}'. Use: {All}", unknownCategory, string.Join(", ", knownCategories.Order()));
            return 2;
        }
        var timeout = TimeSpan.FromMinutes(int.Parse(TakeOption(remaining, "--timeout-minutes") ?? "20", System.Globalization.CultureInfo.InvariantCulture));
        var started = DateTimeOffset.UtcNow;
        var repositoryRoot = RunManifest.FindRepositoryRoot();
        var output = Path.GetFullPath(TakeOption(remaining, "--output") ?? Path.Combine(repositoryRoot, "results", RunManifest.NewRunId(started)));

        bool Include(BenchmarkCaseDefinition c) =>
            (libraries is null || libraries.Contains(c.Library)) &&
            (categories is null || categories.Contains(ValidationPass.ScenarioOf(c.Type).Category));

        if (mode == "list")
        {
            foreach (var type in ValidationPass.BenchmarkTypes())
                foreach (var c in ValidationPass.Cases(type).Where(Include))
                    Console.WriteLine(c.CaseId);
            return 0;
        }

        Directory.CreateDirectory(output);
        var manifestPath = Path.Combine(output, "manifest.json");
        var validationPath = Path.Combine(output, "validation.json");
        if (mode is "run" or "validate")
        {
            var manifest = CreateManifest(started, profile, args, repositoryRoot, output);
            manifest.Save(manifestPath);
            logger.LogInformation("Run {RunId}: profile {Profile}, output {Output}", manifest.RunId, profile.Name, output);
            logger.LogInformation("Validating every case once before measurement (timeout {Timeout} min per case)...", timeout.TotalMinutes);
            var records = ValidationPass.Run(output, timeout, Include, message => logger.LogInformation("{Message}", message));
            ValidationPass.Save(validationPath, records);
            var summary = records.GroupBy(r => r.Status).Select(g => $"{g.Key}: {g.Count()}");
            logger.LogInformation("Validation finished: {Summary}", string.Join(", ", summary));
            if (records.Any(r => r.Status == ResultStatus.TimedOut) && mode == "run")
                logger.LogWarning("A case timed out and its thread may still be running; measure in a fresh process with 'bench'.");
            if (mode == "validate")
                return 0;
        }

        if (!File.Exists(validationPath))
        {
            logger.LogError("No validation.json in {Output}. Run 'validate' first.", output);
            return 3;
        }

        var validated = ValidationPass.Load(validationPath);
        var passed = validated.Count(r => r.Status == ResultStatus.Passed);
        logger.LogInformation("Measuring {Passed} validated cases with BenchmarkDotNet...", passed);
        if (categories is not null)
            remaining.AddRange(["--anyCategories", .. categories.Select(c => knownCategories.First(k => string.Equals(k, c, StringComparison.OrdinalIgnoreCase)))]);
        if (!remaining.Contains("--filter") && !remaining.Contains("-f"))
            remaining.AddRange(["--filter", "*"]);
        var jobFromCommandLine = remaining.Contains("--job") || remaining.Contains("-j");
        var config = BenchmarkConfiguration.Create(profile, Path.Combine(output, "bdn"), validated, libraries, jobFromCommandLine);
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run([.. remaining], config).ToArray();
        var failed = summaries.SelectMany(s => s.Reports).Count(r => !r.Success);
        logger.LogInformation("BenchmarkDotNet finished: {Reports} reports, {Failed} without results. Raw output: {Path}",
            summaries.Sum(s => s.Reports.Length), failed, Path.Combine(output, "bdn"));
        return 0;
    }

    private static RunManifest CreateManifest(DateTimeOffset started, BenchmarkProfile profile, string[] args, string repositoryRoot, string output)
    {
        var existing = Path.Combine(output, "manifest.json");
        var runId = File.Exists(existing) ? RunManifest.Load(existing).RunId : Path.GetFileName(output.TrimEnd(Path.DirectorySeparatorChar));
        var components = new List<ComponentVersion>
        {
            ComponentVersions.Of("UnlimitedPDF", typeof(global::UnlimitedPDF.PdfDocument).Assembly),
            ComponentVersions.Of("PDFsharp", typeof(global::PdfSharp.Pdf.PdfDocument).Assembly),
            ComponentVersions.Of("PDFsharp-MigraDoc", typeof(global::MigraDoc.DocumentObjectModel.Document).Assembly),
            ComponentVersions.Of("PdfPig", typeof(global::UglyToad.PdfPig.PdfDocument).Assembly),
            ComponentVersions.Of("SkiaSharp", typeof(global::SkiaSharp.SKCanvas).Assembly),
            new("Skia native", Safe(() => global::SkiaSharp.SkiaSharpVersion.Native.ToString()), "native library"),
            ComponentVersions.Of("HarfBuzzSharp", typeof(global::HarfBuzzSharp.Buffer).Assembly),
            ComponentVersions.Of("Docnet.Core (PDFium validator)", typeof(global::Docnet.Core.DocLib).Assembly),
            ComponentVersions.Of("BenchmarkDotNet", typeof(BenchmarkRunner).Assembly),
            ComponentVersions.Of("Runner", Assembly.GetExecutingAssembly()),
        };
        return new RunManifest(runId, started, profile.Name.ToString(), args, GitInfo.Capture(repositoryRoot),
            EnvironmentInfo.Capture(Assembly.GetExecutingAssembly()), components, FixtureManifest.Version, FixtureManifest.Sha256(),
            FixtureManifest.FontChecksums());
    }

    private static string Safe(Func<string> value)
    {
        try
        {
            return value();
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException or EntryPointNotFoundException)
        {
            return "unavailable";
        }
    }

    private static string Take(List<string> args, int index)
    {
        var value = args[index];
        args.RemoveAt(index);
        return value;
    }

    private static string? TakeOption(List<string> args, string name)
    {
        var index = args.IndexOf(name);
        if (index < 0)
            return null;
        if (index + 1 >= args.Count)
            throw new ArgumentException($"{name} needs a value.");
        var value = args[index + 1];
        args.RemoveRange(index, 2);
        return value;
    }
}
