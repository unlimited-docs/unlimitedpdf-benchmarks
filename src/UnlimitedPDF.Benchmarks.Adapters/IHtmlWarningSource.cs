namespace UnlimitedPDF.Benchmarks.Adapters;

/// <summary>Converters that report what they skipped. Read outside measured operations.</summary>
public interface IHtmlWarningSource
{
    IReadOnlyList<string> LastWarnings { get; }
}
