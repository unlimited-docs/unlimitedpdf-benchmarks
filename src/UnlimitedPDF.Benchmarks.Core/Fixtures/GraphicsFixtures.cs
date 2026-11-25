namespace UnlimitedPDF.Benchmarks.Core.Fixtures;

public enum ShapeKind
{
    Line,
    StrokedRectangle,
    FilledRectangle,
    Circle,
}

/// <summary>A vector shape in PDF user space. For circles X/Y is the centre and Width the diameter.</summary>
public sealed record Shape(ShapeKind Kind, double X, double Y, double Width, double Height, RgbColor Color, double LineWidth = 0.75);

/// <summary>Lines, stroked and filled rectangles and circles on every page.</summary>
public sealed class VectorDocument
{
    private VectorDocument(IReadOnlyList<IReadOnlyList<Shape>> pages) => Pages = pages;

    public IReadOnlyList<IReadOnlyList<Shape>> Pages { get; }

    public int PageCount => Pages.Count;

    public static string PageLabel(int page) => $"Vector page {page:D5}";

    public static VectorDocument Create(int pages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        var random = new Random(3000 + pages);
        var result = new List<IReadOnlyList<Shape>>(pages);
        for (var p = 0; p < pages; p++)
        {
            var shapes = new List<Shape>();
            for (var i = 0; i <= 20; i++)
                shapes.Add(new Shape(ShapeKind.Line, 50, 100 + i * 32, 495, 0, new RgbColor(180, 180, 180), 0.5));
            for (var i = 0; i < 30; i++)
                shapes.Add(new Shape(ShapeKind.StrokedRectangle, random.Next(50, 480), random.Next(100, 700), random.Next(20, 60),
                    random.Next(15, 40), RandomColor(random)));
            for (var i = 0; i < 20; i++)
                shapes.Add(new Shape(ShapeKind.FilledRectangle, random.Next(50, 480), random.Next(100, 700), random.Next(15, 50),
                    random.Next(10, 35), RandomColor(random)));
            for (var i = 0; i < 20; i++)
            {
                var d = random.Next(10, 40);
                shapes.Add(new Shape(ShapeKind.Circle, random.Next(80, 515), random.Next(120, 700), d, d, RandomColor(random)));
            }
            result.Add(shapes);
        }
        return new VectorDocument(result);
    }

    internal static RgbColor RandomColor(Random random) =>
        new((byte)random.Next(20, 220), (byte)random.Next(20, 220), (byte)random.Next(20, 220));
}

/// <summary>Text lines, one image and filled rectangles per page: only features every generation library supports.</summary>
public sealed class MixedDocument
{
    public const int LinesPerPage = 20;

    /// <summary>Sentence characters after the 11-character line id, so every 11 pt line fits the content width.</summary>
    private static readonly int LineBudget = PageGeometry.MaxCharacters(11, PageGeometry.ContentWidth) - 11;

    public const double ImageX = 50;
    public const double ImageY = 400;
    public const double ImageWidth = 240;
    public const double ImageHeight = 180;

    private MixedDocument(IReadOnlyList<IReadOnlyList<TextLine>> text, IReadOnlyList<IReadOnlyList<Shape>> rectangles, IReadOnlyList<FixtureImage> images)
    {
        Text = text;
        Rectangles = rectangles;
        Images = images;
    }

    public IReadOnlyList<IReadOnlyList<TextLine>> Text { get; }

    public IReadOnlyList<IReadOnlyList<Shape>> Rectangles { get; }

    /// <summary>One distinct JPEG per page.</summary>
    public IReadOnlyList<FixtureImage> Images { get; }

    public int PageCount => Text.Count;

    public static MixedDocument Create(int pages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        var random = new Random(4000 + pages);
        var text = new List<IReadOnlyList<TextLine>>(pages);
        var rects = new List<IReadOnlyList<Shape>>(pages);
        for (var p = 1; p <= pages; p++)
        {
            var lines = new List<TextLine>(LinesPerPage);
            for (var l = 0; l < 10; l++)
                lines.Add(new TextLine($"{TextDocument.LineId(p, l + 1)} {Words.Sentence(random, 6, 9, LineBudget)}", 50, 790 - l * 16, 11));
            for (var l = 10; l < LinesPerPage; l++)
                lines.Add(new TextLine($"{TextDocument.LineId(p, l + 1)} {Words.Sentence(random, 6, 9, LineBudget)}", 50, 360 - (l - 10) * 16, 11));
            text.Add(lines);
            var shapes = new List<Shape>();
            for (var i = 0; i < 10; i++)
                shapes.Add(new Shape(ShapeKind.FilledRectangle, 310 + i % 2 * 120, 560 - i / 2 * 36, 110, 28, VectorDocument.RandomColor(random)));
            rects.Add(shapes);
        }
        var images = Enumerable.Range(0, pages).Select(i => ImageFixtures.Create(1000 + i, ImageFormat.Jpeg)).ToArray();
        return new MixedDocument(text, rects, images);
    }
}
