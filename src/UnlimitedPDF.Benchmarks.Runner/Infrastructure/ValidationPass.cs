using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Provenance;
using UnlimitedPDF.Benchmarks.Core.Validation;

namespace UnlimitedPDF.Benchmarks.Runner.Infrastructure;

/// <summary>The outcome of executing and validating one case once, before any measurement.</summary>
public sealed record ValidationRecord(
    string CaseId,
    string Type,
    string Scenario,
    string Category,
    string Library,
    IReadOnlyDictionary<string, string> Parameters,
    ResultStatus Status,
    string? Reason,
    IReadOnlyList<string> Failures,
    int PageCount,
    long OutputBytes,
    int OutputDocuments,
    double SingleRunMilliseconds,
    IReadOnlyList<string> Validators,
    IReadOnlyList<string> Warnings,
    string? SamplePdf,
    string? SampleImage);

/// <summary>One concrete case: a benchmark type, one library method and one parameter combination.</summary>
public sealed record BenchmarkCaseDefinition(Type Type, MethodInfo Method, IReadOnlyList<(PropertyInfo Property, object Value)> Parameters)
{
    public string Library => Method.Name;

    public string CaseId => CaseKey.Create(Type.Name, Method.Name, Parameters.Select(p => (p.Property.Name, p.Value)));

    public IReadOnlyDictionary<string, string> ParameterStrings =>
        Parameters.ToDictionary(p => p.Property.Name, p => CaseKey.Format(p.Value));
}

public static class CaseKey
{
    public static string Format(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    public static string Create(string type, string method, IEnumerable<(string Name, object Value)> parameters) =>
        $"{type}.{method}({string.Join(", ", parameters.OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => $"{p.Name}={Format(p.Value)}"))})";
}

/// <summary>
/// Discovers every case for the current profile, runs each benchmark method once and validates its output independently.
/// Only cases recorded as <see cref="ResultStatus.Passed"/> are later measured by BenchmarkDotNet.
/// </summary>
public static class ValidationPass
{
    public static IReadOnlyList<Type> BenchmarkTypes() =>
        typeof(ValidationPass).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ScenarioBenchmark).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();

    public static ScenarioAttribute ScenarioOf(Type type) =>
        type.GetCustomAttribute<ScenarioAttribute>() ?? new ScenarioAttribute("Other", type.Name, type.Name);

    public static IEnumerable<NotApplicableAttribute> NotApplicable(Type type) =>
        type.GetCustomAttributes<NotApplicableAttribute>(inherit: true);

    public static IEnumerable<MethodInfo> BenchmarkMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null);

    public static IEnumerable<BenchmarkCaseDefinition> Cases(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParamsSourceAttribute>() is not null || p.GetCustomAttribute<ParamsAttribute>() is not null)
            .OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
        var valueSets = properties.Select(p => ParameterValues(type, p).ToArray()).ToArray();
        IEnumerable<IReadOnlyList<(PropertyInfo, object)>> combinations = [[]];
        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            var values = valueSets[i];
            combinations = combinations.SelectMany(c => values.Select(v => (IReadOnlyList<(PropertyInfo, object)>)[.. c, (property, v)])).ToArray();
        }
        foreach (var combination in combinations)
            foreach (var method in BenchmarkMethods(type))
                yield return new BenchmarkCaseDefinition(type, method, combination);
    }

    private static IEnumerable<object> ParameterValues(Type type, PropertyInfo property)
    {
        if (property.GetCustomAttribute<ParamsAttribute>() is { } fixedValues)
            return fixedValues.Values.Select(v => v!);
        var source = property.GetCustomAttribute<ParamsSourceAttribute>()!.Name;
        var member = type.GetMember(source, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy).First();
        var value = member switch
        {
            PropertyInfo p => p.GetValue(null),
            MethodInfo m => m.Invoke(null, null),
            FieldInfo f => f.GetValue(null),
            _ => null,
        };
        return value is System.Collections.IEnumerable items ? items.Cast<object>() : [];
    }

    public static IReadOnlyList<ValidationRecord> Run(string outputDirectory, TimeSpan timeout, Func<BenchmarkCaseDefinition, bool>? include, Action<string> log)
    {
        var samples = Path.Combine(outputDirectory, "samples");
        var records = new List<ValidationRecord>();
        foreach (var type in BenchmarkTypes())
        {
            var scenario = ScenarioOf(type);
            var cases = Cases(type).Where(c => include is null || include(c)).ToArray();
            if (cases.Length == 0 && include is not null)
                continue;
            foreach (var na in NotApplicable(type))
                records.Add(new ValidationRecord($"{type.Name}.{na.Library}(*)", type.Name, scenario.Name, scenario.Category, na.Library,
                    new Dictionary<string, string>(), ResultStatus.Unsupported, na.Reason, [], 0, 0, 0, 0, [], [], null, null));

            var sampled = new HashSet<string>();
            foreach (var definition in cases)
            {
                var takeSample = sampled.Add(definition.Library);
                var record = RunCase(definition, scenario, timeout, takeSample ? samples : null);
                records.Add(record);
                log($"{record.Status,-16} {record.CaseId} {(record.Status == ResultStatus.Passed ? $"{record.OutputBytes:N0} bytes, {record.PageCount} pages" : record.Reason ?? string.Join("; ", record.Failures.Take(2)))}");
            }
        }
        return records;
    }

    public static ValidationRecord RunCase(BenchmarkCaseDefinition definition, ScenarioAttribute scenario, TimeSpan timeout, string? sampleDirectory)
    {
        ValidationRecord Record(ResultStatus status, string? reason, IReadOnlyList<string>? failures = null, ValidationReport? report = null,
            int documents = 0, double ms = 0, IReadOnlyList<string>? warnings = null, string? pdf = null, string? image = null) =>
            new(definition.CaseId, definition.Type.Name, scenario.Name, scenario.Category, definition.Library, definition.ParameterStrings,
                status, reason, failures ?? report?.Failures ?? [], report?.PageCount ?? 0, report?.Bytes ?? 0, documents, ms,
                report?.Validators ?? [], warnings ?? [], pdf, image);

        ScenarioBenchmark instance;
        try
        {
            instance = (ScenarioBenchmark)Activator.CreateInstance(definition.Type)!;
            foreach (var (property, value) in definition.Parameters)
                property.SetValue(instance, value);
        }
        catch (Exception ex)
        {
            return Record(ResultStatus.ExecutionFailed, $"Could not create the benchmark: {ex.GetBaseException().Message}");
        }

        using (instance)
        {
            if (instance.UnsupportedReason(definition.Library) is { } unsupported)
                return Record(ResultStatus.Unsupported, unsupported);
            if (instance.NotExecutedReason(definition.Library) is { } notExecuted)
                return Record(ResultStatus.NotExecuted, notExecuted);

            var setup = definition.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.GetCustomAttribute<GlobalSetupAttribute>() is { } a && a.Targets.Contains(definition.Library));
            object? output = null;
            Exception? failure = null;
            var elapsed = 0.0;
            var task = Task.Factory.StartNew(() =>
            {
                try
                {
                    if (setup is not null)
                        setup.Invoke(instance, null);
                    else
                        instance.Prepare();
                    var stopwatch = Stopwatch.StartNew();
                    output = definition.Method.Invoke(instance, null);
                    elapsed = stopwatch.Elapsed.TotalMilliseconds;
                }
                catch (TargetInvocationException ex)
                {
                    failure = ex.InnerException ?? ex;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }, TaskCreationOptions.LongRunning);

            if (!task.Wait(timeout))
                return Record(ResultStatus.TimedOut, $"No output within {timeout.TotalMinutes:0} minutes.");
            if (failure is NotSupportedException notSupported)
                return Record(ResultStatus.Unsupported, notSupported.Message);
            if (failure is not null || output is null)
                return Record(ResultStatus.ExecutionFailed, failure is null ? "The method returned null." : $"{failure.GetType().Name}: {failure.Message}");

            string? pdfPath = null, imagePath = null;
            if (sampleDirectory is not null)
            {
                var stem = Path.Combine(sampleDirectory, definition.Type.Name,
                    $"{definition.Library}_{string.Join('_', definition.ParameterStrings.Select(p => $"{p.Key}-{p.Value}"))}");
                Directory.CreateDirectory(Path.GetDirectoryName(stem)!);
                pdfPath = stem + ".pdf";
                imagePath = stem + ".png";
            }

            ValidationReport report;
            try
            {
                report = instance.Validate(definition.Library, output, imagePath);
            }
            catch (Exception ex)
            {
                return Record(ResultStatus.FailedValidation, $"Validation threw {ex.GetType().Name}: {ex.Message}");
            }

            var documents = output switch { byte[] => 1, IReadOnlyList<byte[]> list => list.Count, string => 1, _ => 0 };
            if (pdfPath is not null)
            {
                var bytes = output switch
                {
                    byte[] b => b,
                    IReadOnlyList<byte[]> { Count: > 0 } list => list[0],
                    string directory => File.ReadAllBytes(Path.Combine(directory, definition.Library + ".pdf")),
                    _ => null,
                };
                if (bytes is not null)
                    File.WriteAllBytes(pdfPath, bytes);
                else
                    pdfPath = null;
                if (imagePath is not null && !File.Exists(imagePath))
                    imagePath = null;
            }
            var warnings = instance is Benchmarks.HtmlToPdfBenchmarks html && definition.Library == LibraryIds.UnlimitedPdf
                ? html.LastWarnings
                : [];
            return Record(report.Status, report.Passed ? null : "Output failed independent validation.", report: report, documents: documents,
                ms: elapsed, warnings: warnings, pdf: pdfPath, image: imagePath);
        }
    }

    public static void Save(string path, IReadOnlyList<ValidationRecord> records) =>
        File.WriteAllText(path, JsonSerializer.Serialize(records, RunManifest.JsonOptions));

    public static IReadOnlyList<ValidationRecord> Load(string path) =>
        JsonSerializer.Deserialize<List<ValidationRecord>>(File.ReadAllText(path), RunManifest.JsonOptions) ?? [];
}
