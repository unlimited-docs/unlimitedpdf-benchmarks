using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;

namespace UnlimitedPDF.Benchmarks.Adapters.PdfSharp;

// Inside the namespace so they win over UnlimitedPDF.PdfDocument/PdfPage from the enclosing UnlimitedPDF namespace.
using PdfDocument = global::PdfSharp.Pdf.PdfDocument;
using PdfPage = global::PdfSharp.Pdf.PdfPage;

/// <summary>
/// PDFsharp 6.2.4 (MIT) low-level drawing with XGraphics and page import, using defaults except where noted. XFont objects are
/// created once and reused. PDFsharp always embeds TrueType fonts, so it has no standard-font configuration. Tables, headers and
/// flowing layout belong to MigraDoc (see <see cref="MigraDoc.MigraDocAdapter"/>). Complex-script shaping is not documented and is
/// not claimed. One instance per thread.
/// </summary>
public sealed class PdfSharpAdapter :
    IEmptyDocumentGenerator, ITextDocumentGenerator, IRichTextGenerator, IImageDocumentGenerator, IVectorGraphicsGenerator, IMixedContentGenerator,
    IMultilingualGenerator, IPdfMerger, IPdfSplitter, IPageExtractor, IPageRemover
{
    private readonly Dictionary<(FontFace Face, double Size), XFont> _fonts = [];

    public PdfSharpAdapter() => NotoFontResolver.Install();

    public string LibraryId => LibraryIds.PdfSharp;

    public bool SupportsTextColor => true;

    public byte[] CreateEmptyDocument(int pages)
    {
        using var pdf = new PdfDocument();
        for (var i = 0; i < pages; i++)
            pdf.AddPage();
        return Save(pdf);
    }

    public byte[] CreateTextDocument(TextDocument document)
    {
        using var pdf = BuildText(document);
        return Save(pdf);
    }

    public void SaveTextDocument(TextDocument document, string path)
    {
        using var pdf = BuildText(document);
        pdf.Save(path);
    }

    private PdfDocument BuildText(TextDocument document)
    {
        var pdf = new PdfDocument();
        foreach (var lines in document.Pages)
        {
            var page = pdf.AddPage();
            var height = page.Height.Point;
            using var gfx = XGraphics.FromPdfPage(page);
            foreach (var line in lines)
                gfx.DrawString(line.Text, Font(line.Face, line.FontSize), XBrushes.Black, line.X, height - line.Y);
        }
        return pdf;
    }

    public byte[] CreateRichTextDocument(RichTextDocument document)
    {
        using var pdf = new PdfDocument();
        var brushes = new Dictionary<RgbColor, XBrush>();
        foreach (var lines in document.Pages)
        {
            var page = pdf.AddPage();
            var height = page.Height.Point;
            using var gfx = XGraphics.FromPdfPage(page);
            foreach (var line in lines)
            {
                XBrush brush = XBrushes.Black;
                if (line.Color is { } c && !brushes.TryGetValue(c, out brush!))
                    brushes[c] = brush = new XSolidBrush(XColor.FromArgb(c.R, c.G, c.B));
                gfx.DrawString(line.Text, Font(line.Face, line.FontSize), brush, line.X, height - line.Y);
            }
        }
        return Save(pdf);
    }

    public byte[] CreateImageDocument(ImageDocument document)
    {
        using var pdf = new PdfDocument();
        var images = new List<XImage>(document.Images.Count);
        try
        {
            PdfPage? page = null;
            XGraphics? gfx = null;
            var caption = Font(FontFace.Regular, 10);
            for (var i = 0; i < document.Images.Count; i++)
            {
                if (i % ImageDocument.ImagesPerPage == 0)
                {
                    gfx?.Dispose();
                    page = pdf.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                }
                var (_, x, y, captionY) = ImageDocument.Placement(i);
                var image = XImage.FromStream(new MemoryStream(document.Images[i].Encoded, writable: false));
                images.Add(image);
                var height = page!.Height.Point;
                gfx!.DrawImage(image, x, height - y - ImageDocument.BoxHeight, ImageDocument.BoxWidth, ImageDocument.BoxHeight);
                gfx.DrawString(ImageDocument.Caption(i), caption, XBrushes.Black, x, height - captionY);
            }
            gfx?.Dispose();
            return Save(pdf);
        }
        finally
        {
            foreach (var image in images)
                image.Dispose();
        }
    }

    public byte[] CreateVectorDocument(VectorDocument document)
    {
        using var pdf = new PdfDocument();
        foreach (var shapes in document.Pages)
        {
            var page = pdf.AddPage();
            var h = page.Height.Point;
            using var gfx = XGraphics.FromPdfPage(page);
            foreach (var s in shapes)
            {
                var color = XColor.FromArgb(s.Color.R, s.Color.G, s.Color.B);
                switch (s.Kind)
                {
                    case ShapeKind.Line:
                        gfx.DrawLine(new XPen(color, s.LineWidth), s.X, h - s.Y, s.X + s.Width, h - s.Y - s.Height);
                        break;
                    case ShapeKind.StrokedRectangle:
                        gfx.DrawRectangle(new XPen(color, s.LineWidth), s.X, h - s.Y - s.Height, s.Width, s.Height);
                        break;
                    case ShapeKind.FilledRectangle:
                        gfx.DrawRectangle(new XSolidBrush(color), s.X, h - s.Y - s.Height, s.Width, s.Height);
                        break;
                    case ShapeKind.Circle:
                        gfx.DrawEllipse(new XPen(color, s.LineWidth), s.X - s.Width / 2, h - s.Y - s.Width / 2, s.Width, s.Width);
                        break;
                }
            }
            gfx.DrawString(VectorDocument.PageLabel(pdf.PageCount), Font(FontFace.Regular, 10), XBrushes.Black, 50, h - 60);
        }
        return Save(pdf);
    }

    public byte[] CreateMixedDocument(MixedDocument document)
    {
        using var pdf = new PdfDocument();
        var images = new List<XImage>(document.PageCount);
        try
        {
            for (var p = 0; p < document.PageCount; p++)
            {
                var page = pdf.AddPage();
                var h = page.Height.Point;
                using var gfx = XGraphics.FromPdfPage(page);
                foreach (var line in document.Text[p])
                    gfx.DrawString(line.Text, Font(line.Face, line.FontSize), XBrushes.Black, line.X, h - line.Y);
                var image = XImage.FromStream(new MemoryStream(document.Images[p].Encoded, writable: false));
                images.Add(image);
                gfx.DrawImage(image, MixedDocument.ImageX, h - MixedDocument.ImageY - MixedDocument.ImageHeight,
                    MixedDocument.ImageWidth, MixedDocument.ImageHeight);
                foreach (var r in document.Rectangles[p])
                    gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(r.Color.R, r.Color.G, r.Color.B)), r.X, h - r.Y - r.Height, r.Width, r.Height);
            }
            return Save(pdf);
        }
        finally
        {
            foreach (var image in images)
                image.Dispose();
        }
    }

    public bool Supports(Language language) => language == Language.English;

    public byte[] CreateMultilingualDocument(MultilingualDocument document)
    {
        if (!Supports(document.Language))
            throw new NotSupportedException("PDFsharp 6.2.4 does not document complex-script shaping.");
        using var pdf = new PdfDocument();
        foreach (var lines in document.Pages)
        {
            var page = pdf.AddPage();
            var h = page.Height.Point;
            using var gfx = XGraphics.FromPdfPage(page);
            foreach (var line in lines)
                gfx.DrawString(line.Text, Font(FontFace.Regular, line.FontSize), XBrushes.Black, line.X, h - line.Y);
        }
        return Save(pdf);
    }

    public byte[] Merge(IReadOnlyList<byte[]> inputs)
    {
        using var output = new PdfDocument();
        var sources = new List<PdfDocument>(inputs.Count);
        try
        {
            foreach (var input in inputs)
            {
                var source = PdfReader.Open(new MemoryStream(input, writable: false), PdfDocumentOpenMode.Import);
                sources.Add(source);
                foreach (var page in source.Pages)
                    output.AddPage(page);
            }
            return Save(output);
        }
        finally
        {
            foreach (var source in sources)
                source.Dispose();
        }
    }

    public IReadOnlyList<byte[]> SplitIntoPages(byte[] input)
    {
        using var source = PdfReader.Open(new MemoryStream(input, writable: false), PdfDocumentOpenMode.Import);
        var parts = new byte[source.PageCount][];
        for (var i = 0; i < source.PageCount; i++)
        {
            using var part = new PdfDocument();
            part.AddPage(source.Pages[i]);
            parts[i] = Save(part);
        }
        return parts;
    }

    public IReadOnlyList<byte[]> SplitIntoRanges(byte[] input, IReadOnlyList<(int First, int Last)> ranges)
    {
        using var source = PdfReader.Open(new MemoryStream(input, writable: false), PdfDocumentOpenMode.Import);
        var parts = new byte[ranges.Count][];
        for (var r = 0; r < ranges.Count; r++)
        {
            using var part = new PdfDocument();
            for (var p = ranges[r].First; p <= ranges[r].Last; p++)
                part.AddPage(source.Pages[p - 1]);
            parts[r] = Save(part);
        }
        return parts;
    }

    public byte[] ExtractPages(byte[] input, PageSelection selection)
    {
        using var source = PdfReader.Open(new MemoryStream(input, writable: false), PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();
        foreach (var number in selection.Pages)
            output.AddPage(source.Pages[number - 1]);
        return Save(output);
    }

    public byte[] RemovePages(byte[] input, PageSelection selection)
    {
        using var document = PdfReader.Open(new MemoryStream(input, writable: false), PdfDocumentOpenMode.Modify);
        foreach (var index in selection.Pages.Distinct().OrderByDescending(p => p))
            document.Pages.RemoveAt(index - 1);
        return Save(document);
    }

    private XFont Font(FontFace face, double size)
    {
        if (!_fonts.TryGetValue((face, size), out var font))
        {
            var style = face switch { FontFace.Bold => XFontStyleEx.Bold, FontFace.Italic => XFontStyleEx.Italic, _ => XFontStyleEx.Regular };
            _fonts[(face, size)] = font = new XFont(FontAssets.FamilyName(face), size, style);
        }
        return font;
    }

    private static byte[] Save(PdfDocument pdf)
    {
        using var stream = new MemoryStream();
        pdf.Save(stream, false);
        return stream.ToArray();
    }
}
