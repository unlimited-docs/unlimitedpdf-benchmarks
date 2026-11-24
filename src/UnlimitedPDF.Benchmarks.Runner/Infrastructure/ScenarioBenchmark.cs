using System.Globalization;
using BenchmarkDotNet.Attributes;
using UnlimitedPDF.Benchmarks.Core.Validation;

namespace UnlimitedPDF.Benchmarks.Runner.Infrastructure;

/// <summary>
/// Base for every scenario. Each [Benchmark] method is named after a library id and only runs that library's adapter.
/// Fixtures and adapters are built in <see cref="Prepare"/> (BenchmarkDotNet GlobalSetup), never inside measured methods.
/// The validation pass calls the same methods once and validates the output with <see cref="Validate"/> before any measurement.
/// </summary>
public abstract class ScenarioBenchmark : IDisposable
{
    [GlobalSetup]
    public void GlobalSetup() => Prepare();

    [GlobalCleanup]
    public void GlobalCleanup() => Dispose();

    /// <summary>Builds fixtures and adapters. Must be idempotent per instance.</summary>
    public abstract void Prepare();

    /// <summary>Why <paramref name="library"/> cannot run this case with the current parameters, or null when it can.</summary>
    public virtual string? UnsupportedReason(string library) => null;

    /// <summary>Why a supported case cannot run on this machine (for example a missing browser), or null.</summary>
    public virtual string? NotExecutedReason(string library) => null;

    /// <summary>Validates the value a benchmark method returned, outside any measurement.</summary>
    public abstract ValidationReport Validate(string library, object output, string? thumbnailPath);

    public virtual void Dispose() => GC.SuppressFinalize(this);

    protected static ValidationReport Single(object output, PdfExpectation expectation, string? thumbnailPath) =>
        output is byte[] pdf
            ? PdfValidator.Validate(pdf, expectation, thumbnailPath)
            : throw new InvalidOperationException($"Expected byte[] output but got {output.GetType().Name}.");

    protected static string Invariant(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}

/// <summary>Declares that a library takes no part in a scenario, and why. Recorded as Unsupported, never as a result.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class NotApplicableAttribute(string library, string reason) : Attribute
{
    public string Library { get; } = library;

    public string Reason { get; } = reason;
}

/// <summary>Human-readable scenario metadata used in reports.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ScenarioAttribute(string category, string name, string description) : Attribute
{
    public string Category { get; } = category;

    public string Name { get; } = name;

    public string Description { get; } = description;
}

public static class Categories
{
    public const string Generation = "Generation";
    public const string Layout = "Layout";
    public const string Business = "BusinessDocuments";
    public const string Multilingual = "Multilingual";
    public const string Manipulation = "Manipulation";
    public const string HtmlToPdf = "HtmlToPdf";
    public const string FileOutput = "FileOutput";
}
