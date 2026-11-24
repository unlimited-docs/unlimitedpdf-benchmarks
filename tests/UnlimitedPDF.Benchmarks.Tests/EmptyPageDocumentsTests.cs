using UnlimitedPDF.Benchmarks.Core;

namespace UnlimitedPDF.Benchmarks.Tests;

public class EmptyPageDocumentsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void OutputsAreReadableByIndependentParsers(int pages)
    {
        EmptyPageDocuments.ValidateWithPdfPig(EmptyPageDocuments.UnlimitedPdf(pages), pages);
        EmptyPageDocuments.ValidateWithPdfPig(EmptyPageDocuments.PdfSharp(pages), pages);
        EmptyPageDocuments.ValidateWithPdfSharp(EmptyPageDocuments.PdfPig(pages), pages);
    }

    [Fact]
    public void ValidationRejectsUnexpectedPageCount()
    {
        var bytes = EmptyPageDocuments.UnlimitedPdf(1);
        Assert.Throws<InvalidDataException>(() => EmptyPageDocuments.ValidateWithPdfPig(bytes, 2));
        Assert.Throws<InvalidDataException>(() => EmptyPageDocuments.ValidateWithPdfSharp(bytes, 2));
    }

    [Fact]
    public void GeneratorsRejectInvalidPageCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EmptyPageDocuments.UnlimitedPdf(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => EmptyPageDocuments.PdfSharp(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => EmptyPageDocuments.PdfPig(0));
    }
}
