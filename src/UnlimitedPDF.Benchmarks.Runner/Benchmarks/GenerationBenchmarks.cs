using BenchmarkDotNet.Attributes;
using UnlimitedPDF.Benchmarks.Adapters.PdfPig;
using UnlimitedPDF.Benchmarks.Adapters.PdfSharp;
using UnlimitedPDF.Benchmarks.Adapters.SkiaSharp;
using UnlimitedPDF.Benchmarks.Adapters.UnlimitedPdf;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using UnlimitedPDF.Benchmarks.Core.Profiles;
using UnlimitedPDF.Benchmarks.Core.Validation;
using UnlimitedPDF.Benchmarks.Runner.Infrastructure;

namespace UnlimitedPDF.Benchmarks.Runner.Benchmarks;

/// <summary>Shared adapters for the low-level drawing libraries.</summary>
public abstract class DrawingBenchmark : ScenarioBenchmark
{
    protected UnlimitedPdfAdapter UnlimitedPdfAdapter { get; private set; } = null!;
    protected PdfSharpAdapter PdfSharpAdapter { get; private set; } = null!;
    protected PdfPigAdapter PdfPigAdapter { get; private set; } = null!;
    protected SkiaSharpAdapter SkiaSharpAdapter { get; private set; } = null!;

    protected void CreateAdapters()
    {
        UnlimitedPdfAdapter ??= new UnlimitedPdfAdapter();
        PdfSharpAdapter ??= new PdfSharpAdapter();
        PdfPigAdapter ??= new PdfPigAdapter();
        SkiaSharpAdapter ??= new SkiaSharpAdapter();
    }

    public override void Dispose()
    {
        SkiaSharpAdapter?.Dispose();
        base.Dispose();
    }
}

[BenchmarkCategory(Categories.Generation)]
[Scenario(Categories.Generation, "Empty document", "Blank A4 pages with no content, fully serialized to a byte array: the fixed per-document and per-page cost.")]
[NotApplicable(LibraryIds.MigraDoc, "Flowing layout library; a document without content is not a meaningful MigraDoc workload.")]
public class EmptyDocumentBenchmarks : DrawingBenchmark
{
    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.EmptyPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare() => CreateAdapters();

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateEmptyDocument(Pages);

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.CreateEmptyDocument(Pages);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.CreateEmptyDocument(Pages);

    [Benchmark]
    public byte[] SkiaSharp() => SkiaSharpAdapter.CreateEmptyDocument(Pages);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, new PdfExpectation { Producer = library, PageCount = Pages, ExpectInk = false }, thumbnailPath);
}

[BenchmarkCategory(Categories.Generation)]
[Scenario(Categories.Generation, "Plain text, embedded font", "45 positioned lines of 11 pt Noto Sans per A4 page, fully serialized to a byte array.")]
[NotApplicable(LibraryIds.MigraDoc, "Flowing layout library; measured in the layout scenarios instead of positioned drawing.")]
public class TextBenchmarks : DrawingBenchmark
{
    private TextDocument _document = null!;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.TextPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _document = TextDocument.Create(Pages);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateTextDocument(_document);

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.CreateTextDocument(_document);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.CreateTextDocument(_document);

    [Benchmark]
    public byte[] SkiaSharp() => SkiaSharpAdapter.CreateTextDocument(_document);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Text(library, _document), thumbnailPath);
}

[BenchmarkCategory(Categories.Generation)]
[Scenario(Categories.Generation, "Plain text, standard font (default configuration)", "The same text drawn with the built-in, non-embedded Helvetica where the library offers it.")]
[NotApplicable(LibraryIds.PdfSharp, "PDFsharp 6 always embeds a TrueType font; it has no standard-14 font option.")]
[NotApplicable(LibraryIds.SkiaSharp, "Skia's PDF backend always embeds the typeface it draws with.")]
[NotApplicable(LibraryIds.MigraDoc, "Flowing layout library; not a positioned-drawing API.")]
public class StandardFontTextBenchmarks : DrawingBenchmark
{
    private TextDocument _document = null!;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.StandardFontPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _document = TextDocument.Create(Pages);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateStandardFontTextDocument(_document);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.CreateStandardFontTextDocument(_document);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Text(library, _document, FontExpectation.StandardFontUsed), thumbnailPath);
}

[BenchmarkCategory(Categories.Generation)]
[Scenario(Categories.Generation, "Rich text (sizes and faces)", "Headings and bold, regular and italic lines at 10 to 18 pt with the Noto Sans family; black text.")]
[NotApplicable(LibraryIds.MigraDoc, "Flowing layout library; not a positioned-drawing API.")]
public class RichTextBenchmarks : DrawingBenchmark
{
    private RichTextDocument _document = null!;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.RichTextPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _document = RichTextDocument.Create(Pages, colored: false);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateRichTextDocument(_document);

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.CreateRichTextDocument(_document);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.CreateRichTextDocument(_document);

    [Benchmark]
    public byte[] SkiaSharp() => SkiaSharpAdapter.CreateRichTextDocument(_document);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.RichText(library, _document), thumbnailPath);
}

[BenchmarkCategory(Categories.Generation)]
[Scenario(Categories.Generation, "Coloured rich text", "The rich-text layout with five text colours. No UnlimitedPDF baseline, so no ratios.")]
[NotApplicable(LibraryIds.UnlimitedPdf, "UnlimitedPDF 1.1.0 has no text colour API.")]
[NotApplicable(LibraryIds.MigraDoc, "Flowing layout library; not a positioned-drawing API.")]
public class ColoredTextBenchmarks : DrawingBenchmark
{
    private RichTextDocument _document = null!;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.RichTextPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _document = RichTextDocument.Create(Pages, colored: true);
        CreateAdapters();
    }

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.CreateRichTextDocument(_document);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.CreateRichTextDocument(_document);

    [Benchmark]
    public byte[] SkiaSharp() => SkiaSharpAdapter.CreateRichTextDocument(_document);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.RichText(library, _document), thumbnailPath);
}

[BenchmarkCategory(Categories.Generation)]
[Scenario(Categories.Generation, "Images", "N distinct 320 x 240 px images, four per page at 240 x 180 pt with captions; image bytes are decoded inside the operation.")]
[NotApplicable(LibraryIds.MigraDoc, "Flowing layout library; not a positioned-drawing API.")]
public class ImageBenchmarks : DrawingBenchmark
{
    private ImageDocument _document = null!;

    public static IEnumerable<int> ImageCounts => BenchmarkProfile.Current.ImageCounts;

    public static IEnumerable<ImageFormat> Formats => BenchmarkProfile.Current.ImageFormats;

    [ParamsSource(nameof(ImageCounts))]
    public int Images { get; set; }

    [ParamsSource(nameof(Formats))]
    public ImageFormat Format { get; set; }

    public override void Prepare()
    {
        _document = ImageDocument.Create(Images, Format);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateImageDocument(_document);

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.CreateImageDocument(_document);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.CreateImageDocument(_document);

    [Benchmark]
    public byte[] SkiaSharp() => SkiaSharpAdapter.CreateImageDocument(_document);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Images(library, _document), thumbnailPath);
}

[BenchmarkCategory(Categories.Generation)]
[Scenario(Categories.Generation, "Vector graphics", "21 lines, 30 stroked and 20 filled rectangles and 20 circles per page. No UnlimitedPDF baseline, so no ratios.")]
[NotApplicable(LibraryIds.UnlimitedPdf, "UnlimitedPDF 1.1.0 draws only filled rectangles; it has no line, stroke or curve API.")]
[NotApplicable(LibraryIds.MigraDoc, "Flowing layout library; not a positioned-drawing API.")]
public class VectorBenchmarks : DrawingBenchmark
{
    private VectorDocument _document = null!;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.VectorPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _document = VectorDocument.Create(Pages);
        CreateAdapters();
    }

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.CreateVectorDocument(_document);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.CreateVectorDocument(_document);

    [Benchmark]
    public byte[] SkiaSharp() => SkiaSharpAdapter.CreateVectorDocument(_document);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Vector(library, _document), thumbnailPath);
}

[BenchmarkCategory(Categories.Generation)]
[Scenario(Categories.Generation, "Mixed text, image and shapes", "20 text lines, one distinct JPEG and 10 filled rectangles per page: features every drawing library supports.")]
[NotApplicable(LibraryIds.MigraDoc, "Flowing layout library; not a positioned-drawing API.")]
public class MixedContentBenchmarks : DrawingBenchmark
{
    private MixedDocument _document = null!;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.MixedPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _document = MixedDocument.Create(Pages);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateMixedDocument(_document);

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.CreateMixedDocument(_document);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.CreateMixedDocument(_document);

    [Benchmark]
    public byte[] SkiaSharp() => SkiaSharpAdapter.CreateMixedDocument(_document);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Mixed(library, _document), thumbnailPath);
}

[BenchmarkCategory(Categories.Multilingual)]
[Scenario(Categories.Multilingual, "Multilingual text", "30 lines per page in English, Hindi, Telugu, Tamil or mixed scripts at 14 pt with Noto fonts; text must extract exactly as written.")]
[NotApplicable(LibraryIds.MigraDoc, "Flowing layout library; complex-script shaping not documented.")]
public class MultilingualBenchmarks : DrawingBenchmark
{
    private MultilingualDocument _document = null!;

    public static IEnumerable<Language> LanguageValues => BenchmarkProfile.Current.Languages;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.MultilingualPages;

    [ParamsSource(nameof(LanguageValues))]
    public Language Language { get; set; }

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _document = MultilingualDocument.Create(Language, Pages);
        CreateAdapters();
    }

    public override string? UnsupportedReason(string library) => library switch
    {
        LibraryIds.PdfSharp when Language != Language.English => "PDFsharp 6.2.4 does not document complex-script shaping.",
        LibraryIds.PdfPig when Language != Language.English => "PdfPig 0.1.16 does not document complex-script shaping.",
        _ => null,
    };

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateMultilingualDocument(_document);

    [Benchmark]
    public byte[] PdfSharp() => PdfSharpAdapter.CreateMultilingualDocument(_document);

    [Benchmark]
    public byte[] PdfPig() => PdfPigAdapter.CreateMultilingualDocument(_document);

    [Benchmark]
    public byte[] SkiaSharp() => SkiaSharpAdapter.CreateMultilingualDocument(_document);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Multilingual(library, _document), thumbnailPath);
}

[BenchmarkCategory(Categories.FileOutput)]
[Scenario(Categories.FileOutput, "Plain text written to a file", "The plain-text document saved through each library's file API to a local temporary file (end-to-end, includes OS file I/O).")]
[NotApplicable(LibraryIds.MigraDoc, "Flowing layout library; not a positioned-drawing API.")]
public class FileOutputBenchmarks : DrawingBenchmark
{
    private TextDocument _document = null!;
    private string _directory = null!;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.FileOutputPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _document = TextDocument.Create(Pages);
        _directory = Directory.CreateTempSubdirectory("updf-bench-").FullName;
        CreateAdapters();
    }

    private string PathFor(string library) => Path.Combine(_directory, library + ".pdf");

    [Benchmark(Baseline = true)]
    public string UnlimitedPdf()
    {
        UnlimitedPdfAdapter.SaveTextDocument(_document, PathFor(LibraryIds.UnlimitedPdf));
        return _directory;
    }

    [Benchmark]
    public string PdfSharp()
    {
        PdfSharpAdapter.SaveTextDocument(_document, PathFor(LibraryIds.PdfSharp));
        return _directory;
    }

    [Benchmark]
    public string PdfPig()
    {
        PdfPigAdapter.SaveTextDocument(_document, PathFor(LibraryIds.PdfPig));
        return _directory;
    }

    [Benchmark]
    public string SkiaSharp()
    {
        SkiaSharpAdapter.SaveTextDocument(_document, PathFor(LibraryIds.SkiaSharp));
        return _directory;
    }

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        PdfValidator.Validate(File.ReadAllBytes(PathFor(library)), Expectations.Text(library, _document), thumbnailPath);

    public override void Dispose()
    {
        try
        {
            if (_directory is not null && Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        base.Dispose();
    }
}
