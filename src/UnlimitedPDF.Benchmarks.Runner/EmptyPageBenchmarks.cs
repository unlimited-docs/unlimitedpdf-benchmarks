using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters.Json;
using UnlimitedPDF.Benchmarks.Core;

namespace UnlimitedPDF.Benchmarks.Runner;

[MemoryDiagnoser]
[JsonExporterAttribute.Full]
public class EmptyPageBenchmarks
{
    [Params(1, 10)]
    public int Pages { get; set; }

    [GlobalSetup]
    public void Validate()
    {
        EmptyPageDocuments.ValidateWithPdfPig(EmptyPageDocuments.UnlimitedPdf(Pages), Pages);
        EmptyPageDocuments.ValidateWithPdfPig(EmptyPageDocuments.PdfSharp(Pages), Pages);
        EmptyPageDocuments.ValidateWithPdfSharp(EmptyPageDocuments.PdfPig(Pages), Pages);
    }

    [Benchmark(Baseline = true)]
    public byte[] UnlimitedPdf() => EmptyPageDocuments.UnlimitedPdf(Pages);

    [Benchmark]
    public byte[] PdfSharp() => EmptyPageDocuments.PdfSharp(Pages);

    [Benchmark]
    public byte[] PdfPig() => EmptyPageDocuments.PdfPig(Pages);
}
