using System.Text.RegularExpressions;

namespace UnlimitedPDF.Benchmarks.Core.Validation;

public enum FontExpectation
{
    /// <summary>Fonts are not checked.</summary>
    Any,

    /// <summary>Every font used on every page is embedded (FontFile, FontFile2, FontFile3 or Type 3).</summary>
    AllEmbedded,

    /// <summary>At least one non-embedded standard font is used (the default-font configuration).</summary>
    StandardFontUsed,
}

/// <summary>What an output must satisfy. Text tokens are compared with all whitespace removed.</summary>
public sealed class PdfExpectation
{
    /// <summary>Library that produced the output; it is never the only validator of its own output.</summary>
    public required string Producer { get; init; }

    /// <summary>Exact page count, or null when layout libraries may legitimately paginate differently.</summary>
    public int? PageCount { get; init; }

    public int MinPageCount { get; init; } = 1;

    /// <summary>Expected page size in points (A4 by default); null skips the check.</summary>
    public (double Width, double Height)? PageSize { get; init; } = (595, 842);

    /// <summary>Tokens each page (1-based) must contain.</summary>
    public Func<int, IEnumerable<string>>? PageText { get; init; }

    /// <summary>Tokens that must appear somewhere in the document.</summary>
    public IEnumerable<string>? DocumentText { get; init; }

    /// <summary>Regex whose total match count across all pages must equal <see cref="TokenCount"/>.</summary>
    public Regex? CountedToken { get; init; }

    public int TokenCount { get; init; }

    /// <summary>Minimum number of distinct image XObjects reachable from the pages.</summary>
    public int MinDistinctImages { get; init; }

    public FontExpectation Fonts { get; init; } = FontExpectation.Any;

    /// <summary>Require visible marks on rendered page 1 (always true unless the page is intentionally blank).</summary>
    public bool ExpectInk { get; init; } = true;

    /// <summary>Require saturated colour on rendered page 1 (images or coloured content).</summary>
    public bool ExpectColor { get; init; }

    /// <summary>Characters that must not appear in extracted text, such as '?' substituted for missing glyphs.</summary>
    public string? ForbiddenCharacters { get; init; }
}
