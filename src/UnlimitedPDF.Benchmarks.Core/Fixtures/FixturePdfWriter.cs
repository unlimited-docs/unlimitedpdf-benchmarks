using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace UnlimitedPDF.Benchmarks.Core.Fixtures;

public enum FixtureContent
{
    Text,
    Image,
    Mixed,
}

/// <summary>
/// Writes deterministic input PDFs for manipulation benchmarks without using any benchmarked library, so every library
/// receives byte-identical input that none of them produced. PDF 1.7, classic cross-reference table, Flate streams,
/// standard Helvetica text and DeviceRGB images.
/// </summary>
public static class FixturePdfWriter
{
    public const int ImageWidth = 200;
    public const int ImageHeight = 150;

    public static string Marker(int document, int page) => $"Fixture D{document:D3} P{page:D4}";

    public static byte[] Create(int document, int pages, FixtureContent content)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(document);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        var writer = new Writer();
        var random = new Random(10_000 + document * 31 + pages);

        // Object numbers: 1 catalog, 2 pages, 3 font, then per page: page, content, [image].
        const int catalogId = 1, pagesId = 2, fontId = 3;
        var perPage = content == FixtureContent.Text ? 2 : 3;
        var pageIds = Enumerable.Range(0, pages).Select(i => 4 + i * perPage).ToArray();

        writer.Object(catalogId, $"<< /Type /Catalog /Pages {pagesId} 0 R >>");
        writer.Object(pagesId, $"<< /Type /Pages /Count {pages} /Kids [{string.Join(' ', pageIds.Select(id => $"{id} 0 R"))}] >>");
        writer.Object(fontId, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

        for (var p = 0; p < pages; p++)
        {
            var pageId = pageIds[p];
            var contentId = pageId + 1;
            var imageId = pageId + 2;
            var hasImage = content != FixtureContent.Text;
            var resources = hasImage
                ? $"<< /Font << /F1 {fontId} 0 R >> /XObject << /Im1 {imageId} 0 R >> >>"
                : $"<< /Font << /F1 {fontId} 0 R >> >>";
            writer.Object(pageId, $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 595 842] /Resources {resources} /Contents {contentId} 0 R >>");

            var ops = new StringBuilder();
            ops.Append("BT /F1 16 Tf 50 800 Td (").Append(Marker(document, p + 1)).Append(") Tj ET\n");
            var lines = content switch { FixtureContent.Text => 40, FixtureContent.Mixed => 15, _ => 2 };
            for (var l = 0; l < lines; l++)
                ops.Append(CultureInfo.InvariantCulture, $"BT /F1 10 Tf 50 {770 - l * 14} Td ({Words.Sentence(random, 8, 12)}) Tj ET\n");
            if (content == FixtureContent.Mixed)
            {
                for (var r = 0; r < 6; r++)
                    ops.Append(CultureInfo.InvariantCulture,
                        $"{random.Next(10, 90) / 100.0:0.00} {random.Next(10, 90) / 100.0:0.00} {random.Next(10, 90) / 100.0:0.00} rg {320 + r % 2 * 120} {500 - r / 2 * 40} 100 30 re f\n");
            }
            if (hasImage)
            {
                var (w, h, y) = content == FixtureContent.Image ? (480, 360, 380) : (240, 180, 260);
                ops.Append(CultureInfo.InvariantCulture, $"q {w} 0 0 {h} 50 {y} cm /Im1 Do Q\n");
            }
            writer.Stream(contentId, "", Encoding.ASCII.GetBytes(ops.ToString()));

            if (hasImage)
            {
                var rgb = ImageFixtures.Pixels(20_000 + document * 10_000 + p, ImageWidth, ImageHeight);
                writer.Stream(imageId, $"/Type /XObject /Subtype /Image /Width {ImageWidth} /Height {ImageHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8", rgb);
            }
        }

        return writer.Finish(catalogId);
    }

    private sealed class Writer
    {
        private readonly MemoryStream _output = new();
        private readonly SortedDictionary<int, long> _offsets = [];

        public Writer() => Write("%PDF-1.7\n%âãÏÓ\n", latin1: true);

        public void Object(int id, string body)
        {
            _offsets[id] = _output.Position;
            Write($"{id} 0 obj\n{body}\nendobj\n");
        }

        public void Stream(int id, string dictionaryEntries, byte[] data)
        {
            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(data);
            var bytes = compressed.ToArray();
            _offsets[id] = _output.Position;
            Write($"{id} 0 obj\n<< {dictionaryEntries} /Filter /FlateDecode /Length {bytes.Length} >>\nstream\n");
            _output.Write(bytes);
            Write("\nendstream\nendobj\n");
        }

        public byte[] Finish(int rootId)
        {
            var size = _offsets.Keys.Max() + 1;
            var xref = _output.Position;
            var builder = new StringBuilder();
            builder.Append(CultureInfo.InvariantCulture, $"xref\n0 {size}\n0000000000 65535 f \n");
            for (var id = 1; id < size; id++)
                builder.Append(_offsets.TryGetValue(id, out var offset) ? $"{offset:D10} 00000 n \n" : "0000000000 65535 f \n");
            builder.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {size} /Root {rootId} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            Write(builder.ToString());
            return _output.ToArray();
        }

        private void Write(string text, bool latin1 = false)
        {
            var bytes = latin1 ? Encoding.Latin1.GetBytes(text) : Encoding.ASCII.GetBytes(text);
            _output.Write(bytes);
        }
    }
}

/// <summary>Pages chosen for extract, remove and reorder, given both as an ordered list and as inclusive ranges.</summary>
public sealed record PageSelection(IReadOnlyList<int> Pages, IReadOnlyList<(int First, int Last)> Ranges)
{
    public static PageSelection FromRanges(params (int First, int Last)[] ranges)
    {
        var pages = new List<int>();
        foreach (var (first, last) in ranges)
        {
            var step = first <= last ? 1 : -1;
            for (var p = first; p != last + step; p += step)
                pages.Add(p);
        }
        return new PageSelection(pages, ranges);
    }

    /// <summary>The pages kept, in original order, when <see cref="Pages"/> are removed from a document of <paramref name="pageCount"/> pages.</summary>
    public IReadOnlyList<int> Complement(int pageCount)
    {
        var removed = Pages.ToHashSet();
        return Enumerable.Range(1, pageCount).Where(p => !removed.Contains(p)).ToArray();
    }
}

public enum DocumentSize
{
    Small,
    Large,
}

public static class ManipulationFixtures
{
    public const int MergePagesPerDocument = 5;

    public static int Pages(DocumentSize size) => size == DocumentSize.Small ? 10 : 1000;

    public static PageSelection ExtractSelection(DocumentSize size) => size == DocumentSize.Small
        ? PageSelection.FromRanges((2, 2), (5, 7), (9, 9))
        : PageSelection.FromRanges((1, 50), (500, 549), (951, 1000));

    public static PageSelection ReverseSelection(DocumentSize size) => PageSelection.FromRanges((Pages(size), 1));

    public static IReadOnlyList<byte[]> MergeInputs(int documents, FixtureContent content) =>
        Enumerable.Range(1, documents).Select(d => FixturePdfWriter.Create(d, MergePagesPerDocument, content)).ToArray();
}
