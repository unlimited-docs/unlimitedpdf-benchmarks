using PdfSharp.Pdf.IO;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace UnlimitedPDF.Benchmarks.Core;

public static class EmptyPageDocuments
{
    public static byte[] UnlimitedPdf(int pages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        var document = new global::UnlimitedPDF.PdfDocument();
        for (var i = 0; i < pages; i++)
            document.AddPage();
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    public static byte[] PdfSharp(int pages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        using var document = new PdfSharp.Pdf.PdfDocument();
        for (var i = 0; i < pages; i++)
            document.AddPage();
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    public static byte[] PdfPig(int pages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        var document = new PdfDocumentBuilder();
        for (var i = 0; i < pages; i++)
            document.AddPage(PageSize.A4);
        return document.Build();
    }

    public static void ValidateWithPdfPig(byte[] bytes, int expectedPages)
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(bytes);
        if (document.NumberOfPages != expectedPages)
            throw new InvalidDataException("Unexpected page count.");
        foreach (var page in document.GetPages())
            if (Math.Abs((double)page.Width - 595) > 1 || Math.Abs((double)page.Height - 842) > 1)
                throw new InvalidDataException("Unexpected page dimensions.");
    }

    public static void ValidateWithPdfSharp(byte[] bytes, int expectedPages)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        if (document.PageCount != expectedPages)
            throw new InvalidDataException("Unexpected page count.");
        foreach (var page in document.Pages)
            if (Math.Abs(page.Width.Point - 595) > 1 || Math.Abs(page.Height.Point - 842) > 1)
                throw new InvalidDataException("Unexpected page dimensions.");
    }
}
