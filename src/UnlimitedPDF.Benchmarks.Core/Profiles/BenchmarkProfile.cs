using UnlimitedPDF.Benchmarks.Core.Fixtures;

namespace UnlimitedPDF.Benchmarks.Core.Profiles;

public enum ProfileName
{
    Smoke,
    Quick,
    Standard,
    Full,
    Stress,
}

/// <summary>How BenchmarkDotNet measures a profile (overridden by an explicit --job).</summary>
public enum MeasurementJob
{
    /// <summary>One invocation per case: proves the code runs; not a measurement.</summary>
    Dry,

    /// <summary>BenchmarkDotNet ShortRun: 3 warm-up and 3 measured iterations.</summary>
    Short,

    /// <summary>BenchmarkDotNet's default adaptive job.</summary>
    Default,

    /// <summary>Monitoring strategy for long single operations: 1 warm-up and 5 measured single invocations.</summary>
    Monitoring,
}

/// <summary>
/// Parameter sets per execution profile. Stress contains only the largest, opt-in sizes so that "full" plus "stress" covers every
/// planned size exactly once.
/// </summary>
public sealed record BenchmarkProfile(
    ProfileName Name,
    MeasurementJob Job,
    int[] EmptyPages,
    int[] TextPages,
    int[] StandardFontPages,
    int[] RichTextPages,
    int[] VectorPages,
    int[] ImageCounts,
    ImageFormat[] ImageFormats,
    int[] MixedPages,
    int[] TableRows,
    int[] TableImageRows,
    int[] HeaderFooterPages,
    BusinessDocumentKind[] BusinessKinds,
    int[] InvoiceBatchSizes,
    Language[] Languages,
    int[] MultilingualPages,
    int[] MergeDocuments,
    FixtureContent[] MergeContents,
    int[] SplitPages,
    DocumentSize[] SelectionSizes,
    HtmlSize[] HtmlSizes,
    int[] FileOutputPages,
    ProfilerSettings Profiler)
{
    public const string EnvironmentVariable = "UPDF_BENCH_PROFILE";

    private static readonly BusinessDocumentKind[] AllKinds = Enum.GetValues<BusinessDocumentKind>();
    private static readonly Language[] AllLanguages = Enum.GetValues<Language>();
    private static readonly ImageFormat[] BothFormats = [ImageFormat.Png, ImageFormat.Jpeg];
    private static readonly FixtureContent[] AllContents = Enum.GetValues<FixtureContent>();
    private static readonly HtmlSize[] AllHtmlSizes = Enum.GetValues<HtmlSize>();

    public static BenchmarkProfile Smoke { get; } = new(ProfileName.Smoke, MeasurementJob.Dry,
        EmptyPages: [1], TextPages: [1], StandardFontPages: [1], RichTextPages: [1], VectorPages: [1], ImageCounts: [1], ImageFormats: BothFormats,
        MixedPages: [1], TableRows: [10], TableImageRows: [10], HeaderFooterPages: [10], BusinessKinds: [BusinessDocumentKind.Invoice],
        InvoiceBatchSizes: [10], Languages: AllLanguages, MultilingualPages: [1], MergeDocuments: [2], MergeContents: [FixtureContent.Mixed],
        SplitPages: [10], SelectionSizes: [DocumentSize.Small], HtmlSizes: [HtmlSize.Small], FileOutputPages: [1],
        new ProfilerSettings(ColdStartRuns: 2, SequentialCounts: [20], ConcurrencyLevels: [1, 2], ConcurrentDocuments: 40, LongRunningDocuments: [100], SampleIntervalMs: 50));

    public static BenchmarkProfile Quick { get; } = new(ProfileName.Quick, MeasurementJob.Short,
        EmptyPages: [1], TextPages: [1, 10], StandardFontPages: [1, 10], RichTextPages: [1, 10], VectorPages: [1, 10], ImageCounts: [1, 10], ImageFormats: BothFormats,
        MixedPages: [1, 10], TableRows: [10, 100], TableImageRows: [10], HeaderFooterPages: [10], BusinessKinds: [BusinessDocumentKind.Invoice, BusinessDocumentKind.Payslip],
        InvoiceBatchSizes: [10], Languages: AllLanguages, MultilingualPages: [1], MergeDocuments: [2, 10], MergeContents: [FixtureContent.Mixed],
        SplitPages: [10], SelectionSizes: [DocumentSize.Small], HtmlSizes: [HtmlSize.Small, HtmlSize.Medium], FileOutputPages: [10],
        new ProfilerSettings(ColdStartRuns: 3, SequentialCounts: [100], ConcurrencyLevels: [1, 2, 4], ConcurrentDocuments: 200, LongRunningDocuments: [1000], SampleIntervalMs: 100));

    public static BenchmarkProfile Standard { get; } = new(ProfileName.Standard, MeasurementJob.Default,
        EmptyPages: [1, 10], TextPages: [1, 10, 100], StandardFontPages: [1, 10, 100], RichTextPages: [1, 10, 100], VectorPages: [1, 10, 100], ImageCounts: [1, 10, 100], ImageFormats: BothFormats,
        MixedPages: [1, 10, 100], TableRows: [10, 100, 1000], TableImageRows: [10, 100], HeaderFooterPages: [10, 100], BusinessKinds: AllKinds,
        InvoiceBatchSizes: [100], Languages: AllLanguages, MultilingualPages: [1, 10], MergeDocuments: [2, 10, 50], MergeContents: AllContents,
        SplitPages: [10, 100], SelectionSizes: [DocumentSize.Small, DocumentSize.Large], HtmlSizes: AllHtmlSizes, FileOutputPages: [10, 100],
        new ProfilerSettings(ColdStartRuns: 10, SequentialCounts: [100, 1000], ConcurrencyLevels: [1, 2, 4, 8, 16], ConcurrentDocuments: 1000, LongRunningDocuments: [1000], SampleIntervalMs: 100));

    public static BenchmarkProfile Full { get; } = new(ProfileName.Full, MeasurementJob.Default,
        EmptyPages: [1, 10], TextPages: [1, 10, 50, 100, 500, 1000], StandardFontPages: [1, 10, 100, 1000], RichTextPages: [1, 10, 50, 100], VectorPages: [1, 10, 100],
        ImageCounts: [1, 10, 100], ImageFormats: BothFormats, MixedPages: [1, 10, 50, 100], TableRows: [10, 100, 1000], TableImageRows: [10, 100],
        HeaderFooterPages: [10, 100, 1000], BusinessKinds: AllKinds, InvoiceBatchSizes: [100], Languages: AllLanguages, MultilingualPages: [1, 10, 100],
        MergeDocuments: [2, 10, 50, 100], MergeContents: AllContents, SplitPages: [10, 100, 1000], SelectionSizes: [DocumentSize.Small, DocumentSize.Large],
        HtmlSizes: AllHtmlSizes, FileOutputPages: [10, 100],
        new ProfilerSettings(ColdStartRuns: 10, SequentialCounts: [100, 1000, 10000], ConcurrencyLevels: [1, 2, 4, 8, 16], ConcurrentDocuments: 2000, LongRunningDocuments: [1000], SampleIntervalMs: 100));

    public static BenchmarkProfile Stress { get; } = new(ProfileName.Stress, MeasurementJob.Monitoring,
        EmptyPages: [], TextPages: [5000], StandardFontPages: [], RichTextPages: [500], VectorPages: [1000], ImageCounts: [500], ImageFormats: [ImageFormat.Jpeg],
        MixedPages: [1000], TableRows: [10_000, 100_000], TableImageRows: [1000], HeaderFooterPages: [5000], BusinessKinds: [],
        InvoiceBatchSizes: [1000], Languages: [], MultilingualPages: [], MergeDocuments: [500], MergeContents: [FixtureContent.Mixed],
        SplitPages: [5000], SelectionSizes: [], HtmlSizes: [], FileOutputPages: [1000],
        new ProfilerSettings(ColdStartRuns: 0, SequentialCounts: [], ConcurrencyLevels: [16, 32], ConcurrentDocuments: 10_000, LongRunningDocuments: [10_000], SampleIntervalMs: 250));

    public static IReadOnlyList<BenchmarkProfile> All { get; } = [Smoke, Quick, Standard, Full, Stress];

    public static BenchmarkProfile Parse(string name) =>
        All.FirstOrDefault(p => string.Equals(p.Name.ToString(), name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown profile '{name}'. Use one of: {string.Join(", ", All.Select(p => p.Name.ToString().ToLowerInvariant()))}.");

    /// <summary>The profile selected through <see cref="EnvironmentVariable"/>; standard when unset.</summary>
    public static BenchmarkProfile Current =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } name ? Parse(name) : Standard;
}

/// <summary>Resource-profiler workload sizes for a profile.</summary>
public sealed record ProfilerSettings(
    int ColdStartRuns,
    int[] SequentialCounts,
    int[] ConcurrencyLevels,
    int ConcurrentDocuments,
    int[] LongRunningDocuments,
    int SampleIntervalMs);
