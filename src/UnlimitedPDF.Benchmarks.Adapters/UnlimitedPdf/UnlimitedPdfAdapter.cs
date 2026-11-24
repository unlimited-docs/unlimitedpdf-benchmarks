using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using U = UnlimitedPDF;

namespace UnlimitedPDF.Benchmarks.Adapters.UnlimitedPdf;

/// <summary>
/// UnlimitedPDF 1.1.0 through its public API, following its README: fonts are loaded once with PdfFont and reused, images are
/// loaded from bytes inside each operation. Not supported by the 1.1.0 API and therefore not implemented: text colour, and
/// vector strokes or curves (only filled rectangles exist). Not thread-safe; use one instance per thread.
/// </summary>
public sealed class UnlimitedPdfAdapter :
    IEmptyDocumentGenerator, ITextDocumentGenerator, IStandardFontTextGenerator, IRichTextGenerator, IImageDocumentGenerator, IMixedContentGenerator,
    ITableGenerator, IHeaderFooterGenerator, IBusinessDocumentGenerator, IMultilingualGenerator,
    IPdfMerger, IPdfSplitter, IPageExtractor, IPageRemover, IHtmlToPdfConverter, IHtmlWarningSource
{
    private const double Margin = PageGeometry.Margin;
    private readonly Dictionary<FontFace, U.PdfFont> _fonts;
    private readonly U.PdfHtmlLoadOptions _htmlOptions;

    public UnlimitedPdfAdapter()
    {
        _fonts = FontAssets.AllFaces.ToDictionary(f => f, f => U.PdfFont.FromBytes(FontAssets.Bytes(f)));
        _htmlOptions = new U.PdfHtmlLoadOptions();
        _htmlOptions.Fonts.Add(FontAssets.LatinFamily, _fonts[FontFace.Regular]);
        _htmlOptions.Fonts.Add(FontAssets.LatinFamily, _fonts[FontFace.Bold]);
        _htmlOptions.Fonts.Add(FontAssets.LatinFamily, _fonts[FontFace.Italic]);
    }

    public string LibraryId => LibraryIds.UnlimitedPdf;

    public bool SupportsTextColor => false;

    private U.PdfHtml? _lastHtml;

    /// <summary>Warnings of the last conversion, materialised on demand outside the measured operation.</summary>
    public IReadOnlyList<string> LastWarnings =>
        _lastHtml is null ? [] : _lastHtml.Warnings.Select(w => $"{w.Kind}: {w.Message}").ToArray();

    public byte[] CreateEmptyDocument(int pages)
    {
        var pdf = new U.PdfDocument();
        for (var i = 0; i < pages; i++)
            pdf.AddPage();
        return Save(pdf);
    }

    public byte[] CreateTextDocument(TextDocument document) => Save(BuildText(document, standardFont: false));

    public void SaveTextDocument(TextDocument document, string path) => BuildText(document, standardFont: false).Save(path);

    public byte[] CreateStandardFontTextDocument(TextDocument document) => Save(BuildText(document, standardFont: true));

    private U.PdfDocument BuildText(TextDocument document, bool standardFont)
    {
        var pdf = new U.PdfDocument();
        var font = _fonts[FontFace.Regular];
        foreach (var lines in document.Pages)
        {
            var page = pdf.AddPage();
            foreach (var line in lines)
            {
                if (standardFont)
                    page.AddText(line.Text, line.X, line.Y, line.FontSize);
                else
                    page.AddText(line.Text, font, line.X, line.Y, line.FontSize);
            }
        }
        return pdf;
    }

    public byte[] CreateRichTextDocument(RichTextDocument document)
    {
        if (document.Colored)
            throw new NotSupportedException("UnlimitedPDF 1.1.0 has no text colour API.");
        var pdf = new U.PdfDocument();
        foreach (var lines in document.Pages)
        {
            var page = pdf.AddPage();
            foreach (var line in lines)
                page.AddText(line.Text, _fonts[line.Face], line.X, line.Y, line.FontSize);
        }
        return Save(pdf);
    }

    public byte[] CreateImageDocument(ImageDocument document)
    {
        var pdf = new U.PdfDocument();
        var font = _fonts[FontFace.Regular];
        U.PdfPage? page = null;
        for (var i = 0; i < document.Images.Count; i++)
        {
            var (_, x, y, captionY) = ImageDocument.Placement(i);
            if (i % ImageDocument.ImagesPerPage == 0)
                page = pdf.AddPage();
            var image = U.PdfImage.FromBytes(document.Images[i].Encoded);
            page!.AddImage(image, x, y, ImageDocument.BoxWidth, ImageDocument.BoxHeight);
            page.AddText(ImageDocument.Caption(i), font, x, captionY, 10);
        }
        return Save(pdf);
    }

    public byte[] CreateMixedDocument(MixedDocument document)
    {
        var pdf = new U.PdfDocument();
        var font = _fonts[FontFace.Regular];
        for (var p = 0; p < document.PageCount; p++)
        {
            var page = pdf.AddPage();
            foreach (var line in document.Text[p])
                page.AddText(line.Text, font, line.X, line.Y, line.FontSize);
            page.AddImage(U.PdfImage.FromBytes(document.Images[p].Encoded), MixedDocument.ImageX, MixedDocument.ImageY,
                MixedDocument.ImageWidth, MixedDocument.ImageHeight);
            foreach (var r in document.Rectangles[p])
                page.AddRectangle(r.X, r.Y, r.Width, r.Height, U.PdfColor.FromRgb(r.Color.R, r.Color.G, r.Color.B));
        }
        return Save(pdf);
    }

    public byte[] CreateTableDocument(TableSpec table)
    {
        var pdf = NewFlowDocument();
        AddTable(pdf.Body, table, spacing: 0);
        return Save(pdf);
    }

    public byte[] CreateHeaderFooterDocument(HeaderFooterDocument document)
    {
        var pdf = new U.PdfDocument { MarginTop = 40, MarginBottom = 40, MarginLeft = Margin, MarginRight = Margin };
        pdf.Header.Height = 30;
        pdf.Header.AddText(HeaderFooterDocument.HeaderText, _fonts[FontFace.Bold], 0, 8, 12);
        pdf.Footer.Height = 20;
        pdf.Footer.AddText("Page {page} of {pages}", _fonts[FontFace.Regular], 0, 4, 9, U.PdfTextAlignment.Right);
        var font = _fonts[FontFace.Regular];
        for (var p = 0; p < document.PageCount; p++)
        {
            if (p > 0)
                pdf.Body.PageBreak();
            foreach (var line in document.Pages[p])
                pdf.Body.AddText(line, font, spacing: 2, fontSize: 11);
        }
        return Save(pdf);
    }

    public byte[] CreateBusinessDocument(BusinessDocument document)
    {
        var pdf = NewFlowDocument();
        var body = pdf.Body;
        body.AddText(document.Title, _fonts[FontFace.Bold], fontSize: 18);
        foreach (var (label, value) in document.Fields)
            body.AddText($"{label}: {value}", _fonts[FontFace.Regular], spacing: 3, fontSize: 10);
        foreach (var line in document.PartyLines)
            body.AddText(line, _fonts[FontFace.Regular], spacing: 3, fontSize: 10);
        foreach (var table in document.Tables)
        {
            if (table.Caption is not null)
                body.AddText(table.Caption, _fonts[FontFace.Bold], spacing: 12, fontSize: 12, keepWithNext: true);
            AddTable(body, table, spacing: 6);
        }
        foreach (var note in document.Notes)
            body.AddText(note, _fonts[FontFace.Italic], spacing: 6, fontSize: 9);
        return Save(pdf);
    }

    public bool Supports(Language language) => true;

    public byte[] CreateMultilingualDocument(MultilingualDocument document)
    {
        var pdf = new U.PdfDocument();
        pdf.FontFallback.Add(_fonts[FontFace.Devanagari]);
        pdf.FontFallback.Add(_fonts[FontFace.Telugu]);
        pdf.FontFallback.Add(_fonts[FontFace.Tamil]);
        var latin = _fonts[FontFace.Regular];
        foreach (var lines in document.Pages)
        {
            var page = pdf.AddPage();
            foreach (var line in lines)
                page.AddText(line.Text, latin, line.X, line.Y, line.FontSize);
        }
        return Save(pdf);
    }

    public byte[] Merge(IReadOnlyList<byte[]> inputs)
    {
        var files = new U.PdfFile[inputs.Count];
        for (var i = 0; i < inputs.Count; i++)
            files[i] = U.PdfFile.FromBytes(inputs[i]);
        using var output = new MemoryStream();
        U.PdfOperations.Merge(output, files);
        return output.ToArray();
    }

    public IReadOnlyList<byte[]> SplitIntoPages(byte[] input)
    {
        var file = U.PdfFile.FromBytes(input);
        var streams = new List<MemoryStream>(file.PageCount);
        U.PdfOperations.Split(file, 1, _ =>
        {
            var stream = new MemoryStream();
            streams.Add(stream);
            return stream;
        });
        var parts = new byte[streams.Count][];
        for (var i = 0; i < streams.Count; i++)
        {
            parts[i] = streams[i].ToArray();
            streams[i].Dispose();
        }
        return parts;
    }

    public IReadOnlyList<byte[]> SplitIntoRanges(byte[] input, IReadOnlyList<(int First, int Last)> ranges)
    {
        var file = U.PdfFile.FromBytes(input);
        var pageRanges = new U.PdfPageRange[ranges.Count];
        for (var i = 0; i < ranges.Count; i++)
            pageRanges[i] = new U.PdfPageRange(ranges[i].First, ranges[i].Last);
        var streams = new List<MemoryStream>(ranges.Count);
        U.PdfOperations.Split(file, pageRanges, _ =>
        {
            var stream = new MemoryStream();
            streams.Add(stream);
            return stream;
        });
        var parts = new byte[streams.Count][];
        for (var i = 0; i < streams.Count; i++)
        {
            parts[i] = streams[i].ToArray();
            streams[i].Dispose();
        }
        return parts;
    }

    public byte[] ExtractPages(byte[] input, PageSelection selection)
    {
        var file = U.PdfFile.FromBytes(input);
        using var output = new MemoryStream();
        U.PdfOperations.Extract(file, ToRanges(selection), output);
        return output.ToArray();
    }

    public byte[] RemovePages(byte[] input, PageSelection selection)
    {
        var file = U.PdfFile.FromBytes(input);
        using var output = new MemoryStream();
        U.PdfOperations.Remove(file, ToRanges(selection), output);
        return output.ToArray();
    }

    public byte[] ConvertHtml(HtmlDocument html)
    {
        var source = U.PdfHtml.FromString(html.Html, _htmlOptions);
        using var output = new MemoryStream();
        U.PdfOperations.ConvertHtml(source, output);
        _lastHtml = source;
        return output.ToArray();
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// Ascending ranges map to PdfPageRange directly. PdfPageRange requires first &lt;= last, so a descending range is passed as
    /// single-page ranges in descending order, which is also what PdfPageRange.Parse("9-7") produces.
    /// </summary>
    private static List<U.PdfPageRange> ToRanges(PageSelection selection)
    {
        var ranges = new List<U.PdfPageRange>(selection.Ranges.Count);
        foreach (var (first, last) in selection.Ranges)
        {
            if (first <= last)
                ranges.Add(new U.PdfPageRange(first, last));
            else
                for (var p = first; p >= last; p--)
                    ranges.Add(new U.PdfPageRange(p, p));
        }
        return ranges;
    }

    private static U.PdfDocument NewFlowDocument() =>
        new() { MarginTop = Margin, MarginBottom = Margin, MarginLeft = Margin, MarginRight = Margin };

    private void AddTable(U.PdfBodyFlow body, TableSpec spec, double spacing)
    {
        var table = body.AddTable(spec.Width, spacing);
        table.SetColumnWidths([.. spec.ColumnWidths]);
        table.HeaderRowCount = 1;
        table.RepeatHeaderRows = spec.RepeatHeader;
        var hasImages = spec.Rows.Count > 0 && spec.Rows[0].Image is not null;
        if (hasImages)
            table.RowHeightMode = U.PdfRowHeightMode.AtLeast;
        var bold = _fonts[FontFace.Bold];
        var regular = _fonts[FontFace.Regular];
        for (var c = 0; c < spec.Headers.Count; c++)
        {
            table.SetText(0, c, spec.Headers[c], bold);
            var cell = table.GetCell(0, c);
            cell.BackgroundColor = U.PdfColor.LightGray;
            if (spec.Alignments[c] == CellAlignment.Right)
                cell.HorizontalAlignment = U.PdfCellHorizontalAlignment.Right;
        }
        for (var r = 0; r < spec.Rows.Count; r++)
        {
            var row = spec.Rows[r];
            for (var c = 0; c < row.Cells.Count; c++)
            {
                if (c == 0 && row.Image is not null)
                    table.GetOrCreateCell(r + 1, 0).SetContent(
                        new U.PdfCellImage(U.PdfImage.FromBytes(row.Image.Encoded)), new U.PdfCellText(row.Cells[0], regular));
                else
                    table.SetText(r + 1, c, row.Cells[c], regular);
                if (spec.Alignments[c] == CellAlignment.Right)
                    table.GetCell(r + 1, c).HorizontalAlignment = U.PdfCellHorizontalAlignment.Right;
            }
        }
    }

    private static byte[] Save(U.PdfDocument pdf)
    {
        using var stream = new MemoryStream();
        pdf.Save(stream);
        return stream.ToArray();
    }
}
