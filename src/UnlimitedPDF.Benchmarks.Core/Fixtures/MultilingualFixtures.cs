namespace UnlimitedPDF.Benchmarks.Core.Fixtures;

public enum Language
{
    English,
    Hindi,
    Telugu,
    Tamil,
    Mixed,
}

/// <summary>A run of text that one font can draw.</summary>
public sealed record TextRun(string Text, FontFace Face);

/// <summary>A line made of script runs, drawn left to right at a baseline.</summary>
public sealed record MultilingualLine(IReadOnlyList<TextRun> Runs, double X, double Y, double FontSize)
{
    public string Text => string.Concat(Runs.Select(r => r.Text));
}

/// <summary>Short everyday sentences per script, repeated with a line number. The same lines go to every library.</summary>
public sealed class MultilingualDocument
{
    public const int LinesPerPage = 30;
    public const double FontSize = 14;
    public const double LineHeight = 24;

    private static readonly Dictionary<Language, string[]> Sentences = new()
    {
        [Language.English] =
        [
            "Thank you for your order.", "Please pay within thirty days.", "The shipment left the warehouse today.",
            "This is a test document.", "Your account balance is shown below.",
        ],
        [Language.Hindi] =
        [
            "नमस्ते, आप कैसे हैं?", "यह एक परीक्षण दस्तावेज़ है।", "कृपया तीस दिनों के भीतर भुगतान करें।",
            "कर्मचारी का वेतन विवरण नीचे दिया गया है।", "आपके आदेश के लिए धन्यवाद।",
        ],
        [Language.Telugu] =
        [
            "నమస్కారం, మీరు ఎలా ఉన్నారు?", "ఇది ఒక పరీక్ష పత్రం.", "దయచేసి ముప్పై రోజులలో చెల్లించండి.",
            "భారతదేశం ఒక పెద్ద దేశం.", "మీ సహకారానికి ధన్యవాదాలు.",
        ],
        [Language.Tamil] =
        [
            "வணக்கம், நீங்கள் எப்படி இருக்கிறீர்கள்?", "இது ஒரு சோதனை ஆவணம்.", "தயவுசெய்து முப்பது நாட்களுக்குள் பணம் செலுத்தவும்.",
            "இந்தியா ஒரு பெரிய நாடு.", "உங்கள் ஆர்டருக்கு நன்றி.",
        ],
    };

    private MultilingualDocument(Language language, IReadOnlyList<IReadOnlyList<MultilingualLine>> pages)
    {
        Language = language;
        Pages = pages;
    }

    public Language Language { get; }

    public IReadOnlyList<IReadOnlyList<MultilingualLine>> Pages { get; }

    public int PageCount => Pages.Count;

    public IEnumerable<FontFace> FacesUsed => Pages.SelectMany(p => p).SelectMany(l => l.Runs).Select(r => r.Face).Distinct();

    public static FontFace FaceFor(Language language) => language switch
    {
        Language.Hindi => FontFace.Devanagari,
        Language.Telugu => FontFace.Telugu,
        Language.Tamil => FontFace.Tamil,
        _ => FontFace.Regular,
    };

    public static MultilingualDocument Create(Language language, int pages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        var result = new List<IReadOnlyList<MultilingualLine>>(pages);
        for (var p = 1; p <= pages; p++)
        {
            var lines = new MultilingualLine[LinesPerPage];
            for (var l = 0; l < LinesPerPage; l++)
            {
                var number = new TextRun($"{p:D3}.{l + 1:D2} ", FontFace.Regular);
                IReadOnlyList<TextRun> runs;
                if (language == Language.Mixed)
                {
                    var scripts = new[] { Language.English, Language.Hindi, Language.Telugu, Language.Tamil };
                    var first = scripts[l % 4];
                    var second = scripts[(l + 1) % 4];
                    // Two short sentences (indices 1 and 3) so mixed lines stay inside the 495 pt text width.
                    runs = [number, Run(first, 1 + l % 2 * 2), new TextRun(" | ", FontFace.Regular), Run(second, 3 - l % 2 * 2)];
                }
                else
                {
                    runs = [number, Run(language, l)];
                }
                lines[l] = new MultilingualLine(runs, PageGeometry.Margin, 790 - l * LineHeight, FontSize);
            }
            result.Add(lines);
        }
        return new MultilingualDocument(language, result);
    }

    private static TextRun Run(Language language, int index)
    {
        var sentences = Sentences[language];
        return new TextRun(sentences[index % sentences.Length], FaceFor(language));
    }
}
