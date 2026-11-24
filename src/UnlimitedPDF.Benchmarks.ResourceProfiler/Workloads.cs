using UnlimitedPDF.Benchmarks.Adapters;
using UnlimitedPDF.Benchmarks.Adapters.MigraDoc;
using UnlimitedPDF.Benchmarks.Adapters.PdfPig;
using UnlimitedPDF.Benchmarks.Adapters.PdfSharp;
using UnlimitedPDF.Benchmarks.Adapters.SkiaSharp;
using UnlimitedPDF.Benchmarks.Adapters.UnlimitedPdf;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using UnlimitedPDF.Benchmarks.Core.Validation;

namespace UnlimitedPDF.Benchmarks.ResourceProfiler;

/// <summary>A document-generation workload the profiler repeats. Fixtures are built once per process, outside measurement.</summary>
public sealed record Workload(string Id, string Description, int PagesPerDocument, IReadOnlyList<string> Libraries)
{
    public const string Text = "text-1-page";
    public const string Invoice = "invoice";
    public const string Html = "html-simple-small";

    public static IReadOnlyList<Workload> All { get; } =
    [
        new(Text, "One A4 page of positioned text in embedded Noto Sans", 1,
            [LibraryIds.UnlimitedPdf, LibraryIds.PdfSharp, LibraryIds.PdfPig, LibraryIds.SkiaSharp]),
        new(Invoice, "One flowing invoice with item and totals tables", 1, [LibraryIds.UnlimitedPdf, LibraryIds.MigraDoc]),
        new(Html, "Small simple HTML converted to A4", 1, [LibraryIds.UnlimitedPdf]),
    ];

    public static Workload Get(string id) => All.FirstOrDefault(w => w.Id == id) ?? throw new ArgumentException($"Unknown workload '{id}'.");

    /// <summary>Creates the adapter and the generate function. Called once per worker thread.</summary>
    public (IPdfLibraryAdapter Adapter, Func<byte[]> Generate) Create(string library)
    {
        switch (Id)
        {
            case Text:
            {
                var document = SharedText.Value;
                ITextDocumentGenerator adapter = library switch
                {
                    LibraryIds.UnlimitedPdf => new UnlimitedPdfAdapter(),
                    LibraryIds.PdfSharp => new PdfSharpAdapter(),
                    LibraryIds.PdfPig => new PdfPigAdapter(),
                    LibraryIds.SkiaSharp => new SkiaSharpAdapter(),
                    _ => throw new NotSupportedException(library),
                };
                return (adapter, () => adapter.CreateTextDocument(document));
            }
            case Invoice:
            {
                var document = SharedInvoice.Value;
                IBusinessDocumentGenerator adapter = library switch
                {
                    LibraryIds.UnlimitedPdf => new UnlimitedPdfAdapter(),
                    LibraryIds.MigraDoc => new MigraDocAdapter(),
                    _ => throw new NotSupportedException(library),
                };
                return (adapter, () => adapter.CreateBusinessDocument(document));
            }
            case Html:
            {
                var document = SharedHtml.Value;
                IHtmlToPdfConverter adapter = library switch
                {
                    LibraryIds.UnlimitedPdf => new UnlimitedPdfAdapter(),
                    _ => throw new NotSupportedException(library),
                };
                return (adapter, () => adapter.ConvertHtml(document));
            }
            default:
                throw new NotSupportedException(Id);
        }
    }

    public ValidationReport Validate(string library, byte[] output) => Id switch
    {
        Text => PdfValidator.Validate(output, new PdfExpectation
        {
            Producer = library,
            PageCount = 1,
            PageText = _ => SharedText.Value.Pages[0].Select(l => l.Text),
            Fonts = FontExpectation.AllEmbedded,
        }),
        Invoice => PdfValidator.Validate(output, new PdfExpectation
        {
            Producer = library,
            DocumentText = SharedInvoice.Value.RequiredText().Distinct().ToArray(),
            Fonts = FontExpectation.AllEmbedded,
        }),
        _ => PdfValidator.Validate(output, new PdfExpectation { Producer = library, DocumentText = SharedHtml.Value.RequiredText }),
    };

    private static readonly Lazy<TextDocument> SharedText = new(() => TextDocument.Create(1));
    private static readonly Lazy<BusinessDocument> SharedInvoice = new(() => BusinessDocuments.Create(BusinessDocumentKind.Invoice));
    private static readonly Lazy<HtmlDocument> SharedHtml = new(() => HtmlDocument.Create(HtmlKind.Simple, HtmlSize.Small));
}
