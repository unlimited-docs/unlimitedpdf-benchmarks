using BenchmarkDotNet.Attributes;
using UnlimitedPDF.Benchmarks.Adapters.PdfPig;
using UnlimitedPDF.Benchmarks.Adapters.PdfSharp;
using UnlimitedPDF.Benchmarks.Adapters.UnlimitedPdf;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using UnlimitedPDF.Benchmarks.Core.Profiles;
using UnlimitedPDF.Benchmarks.Core.Validation;
using UnlimitedPDF.Benchmarks.Runner.Infrastructure;

namespace UnlimitedPDF.Benchmarks.Runner.Benchmarks;

/// <summary>
/// Manipulation of byte-identical input PDFs written by <see cref="FixturePdfWriter"/>, which no benchmarked library produced.
/// Parsing the input bytes is part of every measured operation.
/// </summary>
[NotApplicable(LibraryIds.MigraDoc, "Document-generation companion; page manipulation is PDFsharp's job.")]
[NotApplicable(LibraryIds.SkiaSharp, "No API to open or copy pages of existing PDFs.")]
public abstract class ManipulationBenchmark : ScenarioBenchmark
{
    protected UnlimitedPdfAdapter UnlimitedPdfAdapter { get; private set; } = null!;
    protected PdfSharpAdapter PdfSharpAdapter { get; private set; } = null!;
    protected PdfPigAdapter PdfPigAdapter { get; private set; } = null!;

    protected void CreateAdapters()
    {
        UnlimitedPdfAdapter ??= new UnlimitedPdfAdapter();
        PdfSharpAdapter ??= new PdfSharpAdapter();
        PdfPigAdapter ??= new PdfPigAdapter();
    }
}

[BenchmarkCategory(Categories.Manipulation)]
[Scenario(Categories.Manipulation, "Merge PDFs", "Join N five-page input PDFs (text, image or mixed content) into one PDF. UnlimitedPDF's Merge also adds one bookmark per input.")]
public class MergeBenchmarks : ManipulationBenchmark
{
    private IReadOnlyList<byte[]> _inputs = null!;

    public static IEnumerable<int> DocumentCounts => BenchmarkProfile.Current.MergeDocuments;

    public static IEnumerable<FixtureContent> Contents => BenchmarkProfile.Current.MergeContents;

    [ParamsSource(nameof(DocumentCounts))]
    public int Documents { get; set; }

    [ParamsSource(nameof(Contents))]
    public FixtureContent Content { get; set; }

    public override void Prepare()
    {
        _inputs = ManipulationFixtures.MergeInputs(Documents, Content);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.Merge(_inputs);

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.Merge(_inputs);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.Merge(_inputs);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Merged(library, Documents, Content), thumbnailPath);
}

[BenchmarkCategory(Categories.Manipulation)]
[Scenario(Categories.Manipulation, "Split into single pages", "Split one mixed-content PDF of N pages into N single-page PDFs in memory.")]
public class SplitBenchmarks : ManipulationBenchmark
{
    private byte[] _input = null!;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.SplitPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _input = FixturePdfWriter.Create(1, Pages, FixtureContent.Mixed);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<byte[]> UnlimitedPdf() => UnlimitedPdfAdapter.SplitIntoPages(_input);

    [Benchmark]
    public IReadOnlyList<byte[]> PdfSharp() => PdfSharpAdapter.SplitIntoPages(_input);

    [Benchmark]
    public IReadOnlyList<byte[]> PdfPig() => PdfPigAdapter.SplitIntoPages(_input);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath)
    {
        var parts = (IReadOnlyList<byte[]>)output;
        if (parts.Count != Pages)
            return new ValidationReport(ResultStatus.FailedValidation, [$"Expected {Pages} parts but got {parts.Count}."], 0, parts.Sum(p => (long)p.Length), [], 0);
        return ValidationReport.Combine(parts.Select((part, i) =>
            PdfValidator.Validate(part, Expectations.FixturePages(library, 1, [i + 1], FixtureContent.Mixed), i == 0 ? thumbnailPath : null)).ToArray());
    }
}

[BenchmarkCategory(Categories.Manipulation)]
[Scenario(Categories.Manipulation, "Split into page ranges", "Split one mixed-content PDF of N pages into consecutive 10-page parts in memory.")]
public class SplitRangesBenchmarks : ManipulationBenchmark
{
    public const int PagesPerPart = 10;
    private byte[] _input = null!;
    private (int First, int Last)[] _ranges = null!;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.SplitPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _input = FixturePdfWriter.Create(1, Pages, FixtureContent.Mixed);
        _ranges = Enumerable.Range(0, (Pages + PagesPerPart - 1) / PagesPerPart)
            .Select(i => (i * PagesPerPart + 1, Math.Min(Pages, (i + 1) * PagesPerPart))).ToArray();
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<byte[]> UnlimitedPdf() => UnlimitedPdfAdapter.SplitIntoRanges(_input, _ranges);

    [Benchmark]
    public IReadOnlyList<byte[]> PdfSharp() => PdfSharpAdapter.SplitIntoRanges(_input, _ranges);

    [Benchmark]
    public IReadOnlyList<byte[]> PdfPig() => PdfPigAdapter.SplitIntoRanges(_input, _ranges);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath)
    {
        var parts = (IReadOnlyList<byte[]>)output;
        if (parts.Count != _ranges.Length)
            return new ValidationReport(ResultStatus.FailedValidation, [$"Expected {_ranges.Length} parts but got {parts.Count}."], 0, parts.Sum(p => (long)p.Length), [], 0);
        return ValidationReport.Combine(parts.Select((part, i) =>
            PdfValidator.Validate(part, Expectations.FixturePages(library, 1,
                Enumerable.Range(_ranges[i].First, _ranges[i].Last - _ranges[i].First + 1).ToArray(), FixtureContent.Mixed),
                i == 0 ? thumbnailPath : null)).ToArray());
    }
}

public abstract class SelectionBenchmark : ManipulationBenchmark
{
    protected byte[] Input { get; private set; } = null!;

    protected PageSelection Selection { get; private set; } = null!;

    public static IEnumerable<DocumentSize> Sizes => BenchmarkProfile.Current.SelectionSizes;

    [ParamsSource(nameof(Sizes))]
    public DocumentSize Size { get; set; }

    protected abstract PageSelection CreateSelection();

    /// <summary>The source pages the output must contain, in order.</summary>
    protected abstract IReadOnlyList<int> ExpectedPages();

    public override void Prepare()
    {
        Input = FixturePdfWriter.Create(1, ManipulationFixtures.Pages(Size), FixtureContent.Mixed);
        Selection = CreateSelection();
        CreateAdapters();
    }

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.FixturePages(library, 1, ExpectedPages(), FixtureContent.Mixed), thumbnailPath);
}

[BenchmarkCategory(Categories.Manipulation)]
[Scenario(Categories.Manipulation, "Extract pages", "Write selected pages to a new PDF: 5 of 10 pages (small) or 150 of 1,000 pages in three ranges (large).")]
public class ExtractPagesBenchmarks : SelectionBenchmark
{
    protected override PageSelection CreateSelection() => ManipulationFixtures.ExtractSelection(Size);

    protected override IReadOnlyList<int> ExpectedPages() => Selection.Pages;

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.ExtractPages(Input, Selection);

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.ExtractPages(Input, Selection);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.ExtractPages(Input, Selection);
}

[BenchmarkCategory(Categories.Manipulation)]
[Scenario(Categories.Manipulation, "Remove pages", "Write the PDF without the same selected pages; the remaining pages keep their order.")]
public class RemovePagesBenchmarks : SelectionBenchmark
{
    protected override PageSelection CreateSelection() => ManipulationFixtures.ExtractSelection(Size);

    protected override IReadOnlyList<int> ExpectedPages() => Selection.Complement(ManipulationFixtures.Pages(Size));

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.RemovePages(Input, Selection);

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.RemovePages(Input, Selection);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.RemovePages(Input, Selection);
}

[BenchmarkCategory(Categories.Manipulation)]
[Scenario(Categories.Manipulation, "Reorder pages", "Write all pages in reverse order (UnlimitedPDF: Extract with a descending range; others: copy pages in that order).")]
public class ReorderPagesBenchmarks : SelectionBenchmark
{
    protected override PageSelection CreateSelection() => ManipulationFixtures.ReverseSelection(Size);

    protected override IReadOnlyList<int> ExpectedPages() => Selection.Pages;

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.ExtractPages(Input, Selection);

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.ExtractPages(Input, Selection);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.ExtractPages(Input, Selection);
}
