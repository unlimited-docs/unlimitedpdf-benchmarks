using System.Text;

namespace UnlimitedPDF.Benchmarks.Core.Fixtures;

/// <summary>Shared A4 geometry in points, origin bottom-left (PDF user space).</summary>
public static class PageGeometry
{
    public const double Width = 595;
    public const double Height = 842;
    public const double Margin = 50;
    public const double ContentWidth = Width - 2 * Margin;
}

/// <summary>Deterministic word source. <see cref="Random"/> with a seed is stable across .NET versions.</summary>
public static class Words
{
    private static readonly string[] Vocabulary =
    [
        "account", "balance", "benchmark", "budget", "catalog", "client", "column", "contract", "customer", "delivery",
        "document", "estimate", "finance", "forecast", "invoice", "item", "ledger", "margin", "monthly", "order",
        "payment", "period", "portfolio", "price", "product", "quarter", "quantity", "receipt", "region", "report",
        "revenue", "sales", "schedule", "service", "shipment", "statement", "summary", "supplier", "table", "tax",
        "total", "transfer", "update", "value", "vendor", "volume", "warehouse", "yearly", "approval", "review",
    ];

    public static string Sentence(Random random, int minWords, int maxWords)
    {
        var count = random.Next(minWords, maxWords + 1);
        var builder = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
                builder.Append(' ');
            builder.Append(Vocabulary[random.Next(Vocabulary.Length)]);
        }
        return builder.ToString();
    }

    public static string Word(Random random) => Vocabulary[random.Next(Vocabulary.Length)];
}

public readonly record struct RgbColor(byte R, byte G, byte B);

/// <summary>A positioned line of text. Y is the baseline in PDF user space (origin bottom-left).</summary>
public sealed record TextLine(string Text, double X, double Y, double FontSize, FontFace Face = FontFace.Regular, RgbColor? Color = null);

/// <summary>Plain text pages: the same lines, positions and font size for every library.</summary>
public sealed class TextDocument
{
    public const int LinesPerPage = 45;
    public const double FontSize = 11;
    public const double LineHeight = 16;
    public const double FirstBaseline = 790;

    private TextDocument(IReadOnlyList<IReadOnlyList<TextLine>> pages) => Pages = pages;

    public IReadOnlyList<IReadOnlyList<TextLine>> Pages { get; }

    public int PageCount => Pages.Count;

    public static string LineId(int page, int line) => $"P{page:D5}-L{line:D2}";

    public static TextDocument Create(int pages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        var random = new Random(1000 + pages);
        var result = new List<IReadOnlyList<TextLine>>(pages);
        for (var p = 1; p <= pages; p++)
        {
            var lines = new TextLine[LinesPerPage];
            for (var l = 0; l < LinesPerPage; l++)
            {
                var text = $"{LineId(p, l + 1)} {Words.Sentence(random, 7, 10)}";
                lines[l] = new TextLine(text, PageGeometry.Margin, FirstBaseline - l * LineHeight, FontSize);
            }
            result.Add(lines);
        }
        return new TextDocument(result);
    }
}

/// <summary>Headings plus bold, regular and italic lines at several sizes; optionally coloured.</summary>
public sealed class RichTextDocument
{
    private static readonly RgbColor[] Palette =
    [
        new(0x1f, 0x3a, 0x93), new(0xb0, 0x1c, 0x2e), new(0x0f, 0x76, 0x4c), new(0x6b, 0x21, 0xa8), new(0x33, 0x33, 0x33),
    ];

    private RichTextDocument(IReadOnlyList<IReadOnlyList<TextLine>> pages, bool colored)
    {
        Pages = pages;
        Colored = colored;
    }

    public IReadOnlyList<IReadOnlyList<TextLine>> Pages { get; }

    public bool Colored { get; }

    public int PageCount => Pages.Count;

    public static string HeadingText(int page) => $"Section {page:D5} overview";

    public static RichTextDocument Create(int pages, bool colored)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        var random = new Random(2000 + pages);
        var result = new List<IReadOnlyList<TextLine>>(pages);
        for (var p = 1; p <= pages; p++)
        {
            var lines = new List<TextLine>();
            var y = 790.0;
            lines.Add(new TextLine(HeadingText(p), PageGeometry.Margin, y, 18, FontFace.Bold, colored ? Palette[0] : null));
            y -= 30;
            for (var block = 0; block < 6; block++)
            {
                lines.Add(new TextLine($"P{p:D5}-B{block} {Words.Sentence(random, 3, 5)}", PageGeometry.Margin, y, 12, FontFace.Bold,
                    colored ? Palette[(block + 1) % Palette.Length] : null));
                y -= 17;
                for (var r = 0; r < 4; r++)
                {
                    lines.Add(new TextLine(Words.Sentence(random, 8, 11), PageGeometry.Margin, y, 11, FontFace.Regular,
                        colored ? Palette[4] : null));
                    y -= 15;
                }
                lines.Add(new TextLine(Words.Sentence(random, 6, 9), PageGeometry.Margin + 12, y, 10, FontFace.Italic,
                    colored ? Palette[(block + 2) % Palette.Length] : null));
                y -= 26;
            }
            result.Add(lines);
        }
        return new RichTextDocument(result, colored);
    }
}
