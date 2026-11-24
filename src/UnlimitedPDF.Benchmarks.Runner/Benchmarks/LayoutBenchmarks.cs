using BenchmarkDotNet.Attributes;
using UnlimitedPDF.Benchmarks.Adapters.MigraDoc;
using UnlimitedPDF.Benchmarks.Adapters.UnlimitedPdf;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using UnlimitedPDF.Benchmarks.Core.Profiles;
using UnlimitedPDF.Benchmarks.Core.Validation;
using UnlimitedPDF.Benchmarks.Runner.Infrastructure;

namespace UnlimitedPDF.Benchmarks.Runner.Benchmarks;

/// <summary>High-level layout: automatic pagination, repeated table headers, headers and footers.</summary>
[NotApplicable(LibraryIds.PdfSharp, "No table or flowing-layout API; PDFsharp's official layout companion is MigraDoc, measured separately.")]
[NotApplicable(LibraryIds.PdfPig, "No table or flowing-layout API; would require a custom layout implementation.")]
[NotApplicable(LibraryIds.SkiaSharp, "No table or flowing-layout API; would require a custom layout implementation.")]
public abstract class LayoutBenchmark : ScenarioBenchmark
{
    protected UnlimitedPdfAdapter UnlimitedPdfAdapter { get; private set; } = null!;
    protected MigraDocAdapter MigraDocAdapter { get; private set; } = null!;

    protected void CreateAdapters()
    {
        UnlimitedPdfAdapter ??= new UnlimitedPdfAdapter();
        MigraDocAdapter ??= new MigraDocAdapter();
    }
}

[BenchmarkCategory(Categories.Layout)]
[Scenario(Categories.Layout, "Simple table", "A four-column table (bold header with background, right-aligned amounts) that paginates and repeats its header row.")]
public class TableBenchmarks : LayoutBenchmark
{
    private TableSpec _table = null!;

    public static IEnumerable<int> RowCounts => BenchmarkProfile.Current.TableRows;

    [ParamsSource(nameof(RowCounts))]
    public int Rows { get; set; }

    public override void Prepare()
    {
        _table = TableFixtures.Simple(Rows);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateTableDocument(_table);

    [Benchmark]
    public byte[] MigraDoc() => MigraDocAdapter.CreateTableDocument(_table);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Table(library, _table), thumbnailPath);
}

[BenchmarkCategory(Categories.Layout)]
[Scenario(Categories.Layout, "Table with images", "A three-column product table with a distinct 64 x 48 px PNG above the text of each row's first cell.")]
public class TableWithImagesBenchmarks : LayoutBenchmark
{
    private TableSpec _table = null!;

    public static IEnumerable<int> RowCounts => BenchmarkProfile.Current.TableImageRows;

    [ParamsSource(nameof(RowCounts))]
    public int Rows { get; set; }

    public override void Prepare()
    {
        _table = TableFixtures.WithImages(Rows);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateTableDocument(_table);

    [Benchmark]
    public byte[] MigraDoc() => MigraDocAdapter.CreateTableDocument(_table);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Table(library, _table), thumbnailPath);
}

[BenchmarkCategory(Categories.Layout)]
[Scenario(Categories.Layout, "Headers, footers and page numbers", "30 flowing lines per page with a header and a 'Page i of N' footer; an explicit page break after each page's lines.")]
public class HeaderFooterBenchmarks : LayoutBenchmark
{
    private HeaderFooterDocument _document = null!;

    public static IEnumerable<int> PageCounts => BenchmarkProfile.Current.HeaderFooterPages;

    [ParamsSource(nameof(PageCounts))]
    public int Pages { get; set; }

    public override void Prepare()
    {
        _document = HeaderFooterDocument.Create(Pages);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateHeaderFooterDocument(_document);

    [Benchmark]
    public byte[] MigraDoc() => MigraDocAdapter.CreateHeaderFooterDocument(_document);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.HeaderFooter(library, _document), thumbnailPath);
}

[BenchmarkCategory(Categories.Business)]
[Scenario(Categories.Business, "Business documents", "Synthetic invoice, purchase order, quotation, sales report, bank statement, payslip, catalogue, financial and tabular reports with identical data.")]
public class BusinessDocumentBenchmarks : LayoutBenchmark
{
    private BusinessDocument _document = null!;

    public static IEnumerable<BusinessDocumentKind> Kinds => BenchmarkProfile.Current.BusinessKinds;

    [ParamsSource(nameof(Kinds))]
    public BusinessDocumentKind Kind { get; set; }

    public override void Prepare()
    {
        _document = BusinessDocuments.Create(Kind);
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => UnlimitedPdfAdapter.CreateBusinessDocument(_document);

    [Benchmark]
    public byte[] MigraDoc() => MigraDocAdapter.CreateBusinessDocument(_document);

    public override ValidationReport Validate(string library, object output, string? thumbnailPath) =>
        Single(output, Expectations.Business(library, _document), thumbnailPath);
}

[BenchmarkCategory(Categories.Business)]
[Scenario(Categories.Business, "Invoice batch", "N distinct invoices generated one after another, each as its own PDF.")]
public class InvoiceBatchBenchmarks : LayoutBenchmark
{
    private BusinessDocument[] _invoices = null!;

    public static IEnumerable<int> BatchSizes => BenchmarkProfile.Current.InvoiceBatchSizes;

    [ParamsSource(nameof(BatchSizes))]
    public int Documents { get; set; }

    public override void Prepare()
    {
        _invoices = Enumerable.Range(0, Documents).Select(i => BusinessDocuments.Create(BusinessDocumentKind.Invoice, i)).ToArray();
        CreateAdapters();
    }

    [Benchmark(Baseline = true)]
    public byte[][] UnlimitedPdf()
    {
        var results = new byte[_invoices.Length][];
        for (var i = 0; i < _invoices.Length; i++)
            results[i] = UnlimitedPdfAdapter.CreateBusinessDocument(_invoices[i]);
        return results;
    }

    [Benchmark]
    public byte[][] MigraDoc()
    {
        var results = new byte[_invoices.Length][];
        for (var i = 0; i < _invoices.Length; i++)
            results[i] = MigraDocAdapter.CreateBusinessDocument(_invoices[i]);
        return results;
    }

    public override ValidationReport Validate(string library, object output, string? thumbnailPath)
    {
        var parts = (byte[][])output;
        if (parts.Length != _invoices.Length)
            return new ValidationReport(ResultStatus.FailedValidation, [$"Expected {_invoices.Length} documents but got {parts.Length}."], 0, 0, [], 0);
        return ValidationReport.Combine(parts.Select((p, i) =>
            PdfValidator.Validate(p, Expectations.Business(library, _invoices[i]), i == 0 ? thumbnailPath : null)).ToArray());
    }
}
