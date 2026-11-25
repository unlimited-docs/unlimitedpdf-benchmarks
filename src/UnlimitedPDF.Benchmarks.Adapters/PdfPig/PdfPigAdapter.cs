using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using PigDocument = UglyToad.PdfPig.PdfDocument;

namespace UnlimitedPDF.Benchmarks.Adapters.PdfPig;

/// <summary>
/// PdfPig 0.1.16 (Apache-2.0) PdfDocumentBuilder and PdfMerger. TrueType fonts must be added to each builder, so the font file is
/// parsed inside every measured document. PdfPig documents no complex-script shaping and has no table or flowing-layout API. Split, extract and remove copy pages with PdfDocumentBuilder.AddPage(document, pageNumber).
/// </summary>
public sealed class PdfPigAdapter :
    IEmptyDocumentGenerator, ITextDocumentGenerator, IStandardFontTextGenerator, IRichTextGenerator, IImageDocumentGenerator, IVectorGraphicsGenerator,
    IMixedContentGenerator, IMultilingualGenerator, IPdfMerger, IPdfSplitter, IPageExtractor, IPageRemover
{
    public string LibraryId => LibraryIds.PdfPig;

    public bool SupportsTextColor => true;

    public byte[] CreateEmptyDocument(int pages)
    {
        using var builder = new PdfDocumentBuilder();
        for (var i = 0; i < pages; i++)
            builder.AddPage(PageSize.A4);
        return builder.Build();
    }

    public byte[] CreateTextDocument(TextDocument document)
    {
        using var builder = new PdfDocumentBuilder();
        WriteText(builder, document, builder.AddTrueTypeFont(FontAssets.Bytes(FontFace.Regular)));
        return builder.Build();
    }

    public void SaveTextDocument(TextDocument document, string path)
    {
        using var file = File.Create(path);
        using var builder = new PdfDocumentBuilder(file);
        WriteText(builder, document, builder.AddTrueTypeFont(FontAssets.Bytes(FontFace.Regular)));
        builder.Build();
    }

    public byte[] CreateStandardFontTextDocument(TextDocument document)
    {
        using var builder = new PdfDocumentBuilder();
        WriteText(builder, document, builder.AddStandard14Font(Standard14Font.Helvetica));
        return builder.Build();
    }

    private static void WriteText(PdfDocumentBuilder builder, TextDocument document, PdfDocumentBuilder.AddedFont font)
    {
        foreach (var lines in document.Pages)
        {
            var page = builder.AddPage(PageSize.A4);
            foreach (var line in lines)
                page.AddText(line.Text, line.FontSize, new PdfPoint(line.X, line.Y), font);
        }
    }

    public byte[] CreateRichTextDocument(RichTextDocument document)
    {
        using var builder = new PdfDocumentBuilder();
        var fonts = new Dictionary<FontFace, PdfDocumentBuilder.AddedFont>
        {
            [FontFace.Regular] = builder.AddTrueTypeFont(FontAssets.Bytes(FontFace.Regular)),
            [FontFace.Bold] = builder.AddTrueTypeFont(FontAssets.Bytes(FontFace.Bold)),
            [FontFace.Italic] = builder.AddTrueTypeFont(FontAssets.Bytes(FontFace.Italic)),
        };
        foreach (var lines in document.Pages)
        {
            var page = builder.AddPage(PageSize.A4);
            foreach (var line in lines)
            {
                if (line.Color is { } c)
                    page.SetTextAndFillColor(c.R, c.G, c.B);
                page.AddText(line.Text, line.FontSize, new PdfPoint(line.X, line.Y), fonts[line.Face]);
                if (line.Color is not null)
                    page.ResetColor();
            }
        }
        return builder.Build();
    }

    public byte[] CreateImageDocument(ImageDocument document)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddTrueTypeFont(FontAssets.Bytes(FontFace.Regular));
        PdfPageBuilder? page = null;
        for (var i = 0; i < document.Images.Count; i++)
        {
            if (i % ImageDocument.ImagesPerPage == 0)
                page = builder.AddPage(PageSize.A4);
            var (_, x, y, captionY) = ImageDocument.Placement(i);
            var image = document.Images[i];
            var rect = new PdfRectangle(x, y, x + ImageDocument.BoxWidth, y + ImageDocument.BoxHeight);
            if (image.Format == ImageFormat.Png)
                page!.AddPng(image.Encoded, rect);
            else
                page!.AddJpeg(image.Encoded, rect);
            page.AddText(ImageDocument.Caption(i), 10, new PdfPoint(x, captionY), font);
        }
        return builder.Build();
    }

    public byte[] CreateVectorDocument(VectorDocument document)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddTrueTypeFont(FontAssets.Bytes(FontFace.Regular));
        for (var p = 0; p < document.PageCount; p++)
        {
            var page = builder.AddPage(PageSize.A4);
            foreach (var s in document.Pages[p])
            {
                switch (s.Kind)
                {
                    case ShapeKind.Line:
                        page.SetStrokeColor(s.Color.R, s.Color.G, s.Color.B);
                        page.DrawLine(new PdfPoint(s.X, s.Y), new PdfPoint(s.X + s.Width, s.Y + s.Height), s.LineWidth);
                        break;
                    case ShapeKind.StrokedRectangle:
                        page.SetStrokeColor(s.Color.R, s.Color.G, s.Color.B);
                        page.DrawRectangle(new PdfPoint(s.X, s.Y), s.Width, s.Height, s.LineWidth);
                        break;
                    case ShapeKind.FilledRectangle:
                        page.SetTextAndFillColor(s.Color.R, s.Color.G, s.Color.B);
                        page.SetStrokeColor(s.Color.R, s.Color.G, s.Color.B);
                        page.DrawRectangle(new PdfPoint(s.X, s.Y), s.Width, s.Height, 0, fill: true);
                        break;
                    case ShapeKind.Circle:
                        page.SetStrokeColor(s.Color.R, s.Color.G, s.Color.B);
                        page.DrawCircle(new PdfPoint(s.X, s.Y), s.Width, s.LineWidth);
                        break;
                }
            }
            page.ResetColor();
            page.AddText(VectorDocument.PageLabel(p + 1), 10, new PdfPoint(50, PageGeometry.Height - 60), font);
        }
        return builder.Build();
    }

    public byte[] CreateMixedDocument(MixedDocument document)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddTrueTypeFont(FontAssets.Bytes(FontFace.Regular));
        for (var p = 0; p < document.PageCount; p++)
        {
            var page = builder.AddPage(PageSize.A4);
            foreach (var line in document.Text[p])
                page.AddText(line.Text, line.FontSize, new PdfPoint(line.X, line.Y), font);
            page.AddJpeg(document.Images[p].Encoded, new PdfRectangle(MixedDocument.ImageX, MixedDocument.ImageY,
                MixedDocument.ImageX + MixedDocument.ImageWidth, MixedDocument.ImageY + MixedDocument.ImageHeight));
            foreach (var r in document.Rectangles[p])
            {
                page.SetTextAndFillColor(r.Color.R, r.Color.G, r.Color.B);
                page.SetStrokeColor(r.Color.R, r.Color.G, r.Color.B);
                page.DrawRectangle(new PdfPoint(r.X, r.Y), r.Width, r.Height, 0, fill: true);
            }
            page.ResetColor();
        }
        return builder.Build();
    }

    public bool Supports(Language language) => language == Language.English;

    public byte[] CreateMultilingualDocument(MultilingualDocument document)
    {
        if (!Supports(document.Language))
            throw new NotSupportedException("PdfPig 0.1.16 does not document complex-script shaping.");
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddTrueTypeFont(FontAssets.Bytes(FontFace.Regular));
        foreach (var lines in document.Pages)
        {
            var page = builder.AddPage(PageSize.A4);
            foreach (var line in lines)
                page.AddText(line.Text, line.FontSize, new PdfPoint(line.X, line.Y), font);
        }
        return builder.Build();
    }

    public byte[] Merge(IReadOnlyList<byte[]> inputs) => PdfMerger.Merge(inputs);

    public IReadOnlyList<byte[]> SplitIntoPages(byte[] input)
    {
        using var source = PigDocument.Open(input);
        var parts = new byte[source.NumberOfPages][];
        for (var i = 1; i <= source.NumberOfPages; i++)
        {
            using var builder = new PdfDocumentBuilder();
            builder.AddPage(source, i);
            parts[i - 1] = builder.Build();
        }
        return parts;
    }

    public IReadOnlyList<byte[]> SplitIntoRanges(byte[] input, IReadOnlyList<(int First, int Last)> ranges)
    {
        using var source = PigDocument.Open(input);
        var parts = new byte[ranges.Count][];
        for (var r = 0; r < ranges.Count; r++)
        {
            using var builder = new PdfDocumentBuilder();
            for (var p = ranges[r].First; p <= ranges[r].Last; p++)
                builder.AddPage(source, p);
            parts[r] = builder.Build();
        }
        return parts;
    }

    public byte[] ExtractPages(byte[] input, PageSelection selection) => CopyPages(input, selection.Pages);

    public byte[] RemovePages(byte[] input, PageSelection selection)
    {
        using var source = PigDocument.Open(input);
        var keep = selection.Complement(source.NumberOfPages);
        using var builder = new PdfDocumentBuilder();
        foreach (var number in keep)
            builder.AddPage(source, number);
        return builder.Build();
    }

    private static byte[] CopyPages(byte[] input, IReadOnlyList<int> pages)
    {
        using var source = PigDocument.Open(input);
        using var builder = new PdfDocumentBuilder();
        foreach (var number in pages)
            builder.AddPage(source, number);
        return builder.Build();
    }
}
