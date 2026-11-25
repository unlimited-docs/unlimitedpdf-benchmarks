using BenchmarkDotNet.Attributes;
using UnlimitedPDF.Benchmarks.Adapters.UnlimitedPdf;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using UnlimitedPDF.Benchmarks.Core.Profiles;
using UnlimitedPDF.Benchmarks.Core.Validation;
using UnlimitedPDF.Benchmarks.Runner.Infrastructure;

namespace UnlimitedPDF.Benchmarks.Runner.Benchmarks;

[BenchmarkCategory(Categories.HtmlToPdf)]
[Scenario(Categories.HtmlToPdf, "HTML to PDF (warm)", "Self-contained HTML with common CSS converted to A4 with 36 pt margins and Noto Sans registered through the font API. No eligible competitor: browser-based converters such as Playwright are excluded (see docs/library-licenses.md), so results are absolute values without ratios.")]
[NotApplicable(LibraryIds.PdfSharp, "No HTML renderer.")]
[NotApplicable(LibraryIds.MigraDoc, "No HTML renderer.")]
[NotApplicable(LibraryIds.PdfPig, "No HTML renderer.")]
[NotApplicable(LibraryIds.SkiaSharp, "No HTML renderer.")]
public class HtmlToPdfBenchmarks : ScenarioBenchmark
{
    private HtmlDocument _html = null!;
    private UnlimitedPdfAdapter? _unlimited;

    public static IEnumerable<HtmlKind> Kinds => Enum.GetValues<HtmlKind>();

    public static IEnumerable<HtmlSize> Sizes => BenchmarkProfile.Current.HtmlSizes;

    [ParamsSource(nameof(Kinds))]
    public HtmlKind Kind { get; set; }

    [ParamsSource(nameof(Sizes))]
    public HtmlSize Size { get; set; }

    /// <summary>Last conversion warnings of UnlimitedPDF, for the validation record.</summary>
    public IReadOnlyList<string> LastWarnings => _unlimited?.LastWarnings ?? [];

    public override void Prepare()
    {
        _html = HtmlDocument.Create(Kind, Size);
        _unlimited ??= new UnlimitedPdfAdapter();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => _unlimited!.ConvertHtml(_html);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Html(library, _html), thumbnailPath);

    public override void Dispose()
    {
        _unlimited?.Dispose();
        base.Dispose();
    }
}
