namespace UnlimitedPDF.Benchmarks.Core;

/// <summary>Stable library identifiers. Benchmark method names use the same identifiers.</summary>
public static class LibraryIds
{
    public const string UnlimitedPdf = "UnlimitedPdf";
    public const string PdfSharp = "PdfSharp";
    public const string MigraDoc = "MigraDoc";
    public const string PdfPig = "PdfPig";
    public const string SkiaSharp = "SkiaSharp";

    public const string Baseline = UnlimitedPdf;

    public static readonly IReadOnlyList<string> All = [UnlimitedPdf, PdfSharp, MigraDoc, PdfPig, SkiaSharp];
}

/// <summary>Licensing facts for each benchmarked package, verified on <see cref="LibraryCatalog.VerifiedOn"/>.</summary>
public sealed record LibraryInfo(
    string Id,
    string DisplayName,
    string PackageId,
    string Version,
    string LatestStableVersion,
    string ReleaseDate,
    string Spdx,
    string LicenseUrl,
    string SourceUrl,
    string Notes);

public static class LibraryCatalog
{
    public const string VerifiedOn = "2026-11-24";

    public static readonly IReadOnlyList<LibraryInfo> All =
    [
        new(LibraryIds.UnlimitedPdf, "UnlimitedPDF", "UnlimitedPDF", "1.1.0", "1.1.0", "2026-11-18", "Apache-2.0",
            "https://github.com/unlimited-docs/UnlimitedPDF/blob/b457b0713a56e85be8e0a7a09a1a6dd4d4bffbd7/LICENSE",
            "https://github.com/unlimited-docs/UnlimitedPDF", "Baseline. Pure managed; depends on Microsoft.AspNetCore.Components.Web (MIT)."),
        new(LibraryIds.PdfSharp, "PDFsharp", "PDFsharp", "6.2.4", "6.5.0", "2026-01-06", "MIT",
            "https://github.com/empira/PDFsharp/blob/v6.2.4/LICENSE",
            "https://github.com/empira/PDFsharp", "6.5.0 and later are AGPL-3.0-only, so the last MIT release is used."),
        new(LibraryIds.MigraDoc, "PDFsharp-MigraDoc", "PDFsharp-MigraDoc", "6.2.4", "6.5.0", "2026-01-06", "MIT",
            "https://github.com/empira/PDFsharp/blob/v6.2.4/LICENSE",
            "https://github.com/empira/PDFsharp", "Official PDFsharp layout companion, not an independent library."),
        new(LibraryIds.PdfPig, "PdfPig", "PdfPig", "0.1.16", "0.1.16", "2026-08-22", "Apache-2.0",
            "https://github.com/UglyToad/PdfPig/blob/a7bb35662bbbf405efddad50aedc9bcdcf515afc/LICENSE",
            "https://github.com/UglyToad/PdfPig", "Licence also carries PDFBox, Adobe font metrics and CMap notices."),
        new(LibraryIds.SkiaSharp, "SkiaSharp", "SkiaSharp", "4.157.0", "4.157.0", "2026-11-18", "MIT",
            "https://github.com/mono/SkiaSharp/blob/release/4.157.0/LICENSE.md",
            "https://github.com/mono/SkiaSharp", "Native Skia (BSD-3-Clause) and bundled components; HarfBuzzSharp (MIT) for complex-script shaping."),
    ];

    public static LibraryInfo Get(string id) => All.First(l => l.Id == id);

    /// <summary>Candidates reviewed and excluded from the comparison, with the reason. Shown in reports and docs.</summary>
    public static readonly IReadOnlyList<(string Package, string Reason)> Excluded =
    [
        ("Microsoft.Playwright 1.65.1 (MIT wrapper)", "HTML-to-PDF needs a separately downloaded Chromium browser and the bundled Node.js driver, run as external processes; the suite only compares self-contained NuGet libraries, so it is excluded and UnlimitedPDF's HTML-to-PDF results have no competitor."),
        ("PDFsharp / PDFsharp-MigraDoc 6.5.0 and later", "AGPL-3.0-only; outside the permissive licensing policy (6.2.4, MIT, is used instead)."),
        ("QuestPDF (current)", "Community licence with revenue-based eligibility, not OSI-approved; outside the licensing policy."),
        ("iText (current)", "AGPL-3.0 or commercial; outside the permissive licensing policy."),
    ];
}
