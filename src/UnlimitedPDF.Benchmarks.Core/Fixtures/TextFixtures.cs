using System.Text;

namespace UnlimitedPDF.Benchmarks.Core.Fixtures;

/// <summary>Shared A4 geometry in points, origin bottom-left (PDF user space).</summary>
public static class PageGeometry
{
    public const double Width = 595;
    public const double Height = 842;
    public const double Margin = 50;
    public const double ContentWidth = Width - 2 * Margin;

    /// <summary>
    /// Upper bound of the Noto Sans advance per character, in em, for the fixture vocabulary, line ids and spaces. The widest
    /// case is "summary " in Noto Sans Bold at 0.622 em per character; Regular and Italic are narrower.
    /// </summary>
    public const double MaxEmPerCharacter = 0.64;

    /// <summary>The most characters that are guaranteed to fit in <paramref name="width"/> points at <paramref name="fontSize"/>.</summary>
    public static int MaxCharacters(double fontSize, double width) => (int)(width / (fontSize * MaxEmPerCharacter));
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

    public static string Sentence(Random random, int minWords, int maxWords) => Sentence(random, minWords, maxWords, int.MaxValue);

    /// <summary>
    /// A sentence of at most <paramref name="maxCharacters"/> characters. Every word is still drawn from
    /// <paramref name="random"/>, so later sentences do not depend on where this one was cut; at least one word is kept.
    /// </summary>
    public static string Sentence(Random random, int minWords, int maxWords, int maxCharacters)
    {
        var count = random.Next(minWords, maxWords + 1);
        var builder = new StringBuilder();
        var full = false;
        for (var i = 0; i < count; i++)
        {
            var word = Vocabulary[random.Next(Vocabulary.Length)];
            if (full)
                continue;
            if (i > 0 && builder.Length + 1 + word.Length > maxCharacters)
            {
                full = true;
                continue;
            }
            if (i > 0)
                builder.Append(' ');
            builder.Append(word);
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
                var id = LineId(p, l + 1);
                var text = $"{id} {Words.Sentence(random, 7, 10, PageGeometry.MaxCharacters(FontSize, PageGeometry.ContentWidth) - id.Length - 1)}";
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
                var heading = $"P{p:D5}-B{block} ";
                lines.Add(new TextLine(heading + Words.Sentence(random, 3, 5, PageGeometry.MaxCharacters(12, PageGeometry.ContentWidth) - heading.Length),
                    PageGeometry.Margin, y, 12, FontFace.Bold,
                    colored ? Palette[(block + 1) % Palette.Length] : null));
                y -= 17;
                for (var r = 0; r < 4; r++)
                {
                    lines.Add(new TextLine(Words.Sentence(random, 8, 11, PageGeometry.MaxCharacters(11, PageGeometry.ContentWidth)), PageGeometry.Margin, y, 11, FontFace.Regular,
                        colored ? Palette[4] : null));
                    y -= 15;
                }
                lines.Add(new TextLine(Words.Sentence(random, 6, 9, PageGeometry.MaxCharacters(10, PageGeometry.ContentWidth - 12)), PageGeometry.Margin + 12, y, 10, FontFace.Italic,
                    colored ? Palette[(block + 2) % Palette.Length] : null));
                y -= 26;
            }
            result.Add(lines);
        }
        return new RichTextDocument(result, colored);
    }
}
