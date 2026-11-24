using System.Globalization;
using System.Text;
using Docnet.Core;
using Docnet.Core.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using UnlimitedPDF.Benchmarks.Core.Imaging;

namespace UnlimitedPDF.Benchmarks.Core.Validation;

public sealed record ValidationReport(
    ResultStatus Status,
    IReadOnlyList<string> Failures,
    int PageCount,
    long Bytes,
    IReadOnlyList<string> Validators,
    double InkRatio)
{
    public bool Passed => Status == ResultStatus.Passed;

    public static ValidationReport Combine(IReadOnlyList<ValidationReport> parts)
    {
        var failures = parts.SelectMany((p, i) => p.Failures.Select(f => $"output {i + 1}: {f}")).Take(50).ToArray();
        return new ValidationReport(failures.Length == 0 ? ResultStatus.Passed : ResultStatus.FailedValidation, failures,
            parts.Sum(p => p.PageCount), parts.Sum(p => p.Bytes), parts.SelectMany(p => p.Validators).Distinct().ToArray(),
            parts.Count == 0 ? 0 : parts.Average(p => p.InkRatio));
    }
}

/// <summary>
/// Independent output validation. PDFium (through Docnet.Core, never benchmarked) checks readability, page count, page size,
/// extracted text and a rendered page for every producer. PDFsharp inspects font embedding and images, except for PDFsharp's
/// and MigraDoc's own output, which PdfPig inspects instead.
/// </summary>
public static class PdfValidator
{
    public const string PdfiumValidator = "PDFium via Docnet.Core 2.6.0";
    private const int MaxFailures = 20;
    private static readonly Lock PdfiumLock = new();

    public static ValidationReport Validate(byte[] pdf, PdfExpectation expectation, string? thumbnailPath = null)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(expectation);
        var failures = new List<string>();
        var validators = new List<string> { PdfiumValidator };
        var pageCount = 0;
        var ink = 0.0;

        if (pdf.Length < 8 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            failures.Add("Output does not start with a PDF header.");
        else
        {
            try
            {
                (pageCount, ink) = CheckWithPdfium(pdf, expectation, failures, thumbnailPath);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                failures.Add($"PDFium could not read the output: {ex.GetType().Name}: {ex.Message}");
            }

            if (expectation.Fonts != FontExpectation.Any || expectation.MinDistinctImages > 0)
            {
                var selfProduced = expectation.Producer is LibraryIds.PdfSharp or LibraryIds.MigraDoc;
                try
                {
                    var structure = selfProduced ? PdfPigStructure.Inspect(pdf) : PdfSharpStructure.Inspect(pdf);
                    validators.Add(selfProduced ? "PdfPig 0.1.16 structure" : "PDFsharp 6.2.4 structure");
                    CheckStructure(structure, expectation, failures);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    failures.Add($"Structure inspection failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        var status = failures.Count == 0 ? ResultStatus.Passed : ResultStatus.FailedValidation;
        return new ValidationReport(status, failures.Take(MaxFailures).ToArray(), pageCount, pdf.LongLength, validators, ink);
    }

    /// <summary>Removes whitespace and invisible formatting characters and applies NFC, so layout differences do not matter.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormC))
        {
            if (char.IsWhiteSpace(c) || c is '\u00AD' or '\u200B' or '\u200C' or '\u200D' or '\uFEFF' or '\0')
                continue;
            builder.Append(c);
        }
        return builder.ToString();
    }

    private static (int Pages, double Ink) CheckWithPdfium(byte[] pdf, PdfExpectation e, List<string> failures, string? thumbnailPath)
    {
        lock (PdfiumLock)
        {
            using var reader = DocLib.Instance.GetDocReader(pdf, new PageDimensions(1.0));
            var pages = reader.GetPageCount();
            if (e.PageCount is { } exact && pages != exact)
                failures.Add($"Expected {exact} pages but found {pages}.");
            if (pages < e.MinPageCount)
                failures.Add($"Expected at least {e.MinPageCount} pages but found {pages}.");

            var documentText = e.DocumentText is not null || e.CountedToken is not null ? new StringBuilder() : null;
            var ink = 0.0;
            for (var i = 0; i < pages; i++)
            {
                using var page = reader.GetPageReader(i);
                if (e.PageSize is { } size && failures.Count < MaxFailures &&
                    (Math.Abs(page.GetPageWidth() - size.Width) > 1.5 || Math.Abs(page.GetPageHeight() - size.Height) > 1.5))
                    failures.Add($"Page {i + 1} is {page.GetPageWidth()} x {page.GetPageHeight()} pt, expected {size.Width} x {size.Height}.");

                string? raw = null;
                if (e.PageText is not null || documentText is not null)
                    raw = page.GetText() ?? string.Empty;
                if (e.PageText is not null && raw is not null)
                {
                    var normalized = Normalize(raw);
                    foreach (var token in e.PageText(i + 1))
                    {
                        if (failures.Count >= MaxFailures)
                            break;
                        if (!normalized.Contains(Normalize(token), StringComparison.Ordinal))
                            failures.Add($"Page {i + 1} is missing text \"{Truncate(token)}\".");
                    }
                }
                documentText?.Append(raw).Append('\n');

                if (i == 0)
                {
                    var (width, height) = (page.GetPageWidth(), page.GetPageHeight());
                    var bgra = page.GetImage();
                    var (inkRatio, colorRatio) = MeasureInk(bgra);
                    ink = inkRatio;
                    if (e.ExpectInk && inkRatio < 0.0005)
                        failures.Add($"Rendered page 1 is blank (ink ratio {inkRatio.ToString("0.00000", CultureInfo.InvariantCulture)}).");
                    if (e.ExpectColor && colorRatio < 0.002)
                        failures.Add("Rendered page 1 has no coloured content although images or colour were expected.");
                    if (thumbnailPath is not null)
                        WriteThumbnail(bgra, width, height, thumbnailPath);
                }
            }

            if (documentText is not null)
            {
                var all = documentText.ToString();
                if (e.DocumentText is not null)
                {
                    var normalized = Normalize(all);
                    foreach (var token in e.DocumentText)
                    {
                        if (failures.Count >= MaxFailures)
                            break;
                        if (!normalized.Contains(Normalize(token), StringComparison.Ordinal))
                            failures.Add($"Document is missing text \"{Truncate(token)}\".");
                    }
                }
                if (e.CountedToken is not null)
                {
                    var count = e.CountedToken.Count(all);
                    if (count != e.TokenCount)
                        failures.Add($"Expected {e.TokenCount} matches of /{e.CountedToken}/ but found {count}.");
                }
            }
            return (pages, ink);
        }
    }

    private static void CheckStructure(PdfStructure structure, PdfExpectation e, List<string> failures)
    {
        if (structure.DistinctImages < e.MinDistinctImages)
            failures.Add($"Expected at least {e.MinDistinctImages} distinct images but found {structure.DistinctImages}.");
        switch (e.Fonts)
        {
            case FontExpectation.AllEmbedded when structure.Fonts.Count == 0:
                failures.Add("No fonts were found although embedded fonts were expected.");
                break;
            case FontExpectation.AllEmbedded when structure.Fonts.Any(f => !f.Embedded):
                failures.Add($"Fonts not embedded: {string.Join(", ", structure.Fonts.Where(f => !f.Embedded).Select(f => f.Name).Distinct())}.");
                break;
            case FontExpectation.StandardFontUsed when !structure.Fonts.Any(f => !f.Embedded):
                failures.Add("Expected a non-embedded standard font, but every font is embedded.");
                break;
        }
    }

    private static (double Ink, double Color) MeasureInk(byte[] bgra)
    {
        long inked = 0, colored = 0, total = bgra.Length / 4;
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            var a = bgra[i + 3];
            if (a < 16)
                continue;
            // Composite over white.
            int b = 255 - (255 - bgra[i]) * a / 255, g = 255 - (255 - bgra[i + 1]) * a / 255, r = 255 - (255 - bgra[i + 2]) * a / 255;
            if (Math.Min(r, Math.Min(g, b)) < 230)
                inked++;
            if (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) > 60)
                colored++;
        }
        return total == 0 ? (0, 0) : ((double)inked / total, (double)colored / total);
    }

    private static void WriteThumbnail(byte[] bgra, int width, int height, string path)
    {
        const int step = 2;
        int w = width / step, h = height / step;
        var rgb = new byte[w * h * 3];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var s = ((y * step) * width + x * step) * 4;
                var d = (y * w + x) * 3;
                var a = bgra[s + 3];
                rgb[d] = (byte)(255 - (255 - bgra[s + 2]) * a / 255);
                rgb[d + 1] = (byte)(255 - (255 - bgra[s + 1]) * a / 255);
                rgb[d + 2] = (byte)(255 - (255 - bgra[s]) * a / 255);
            }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, PngEncoder.EncodeRgb(w, h, rgb));
    }

    private static string Truncate(string token) => token.Length <= 60 ? token : token[..57] + "...";
}

public sealed record FontUse(string Name, bool Embedded);

public sealed record PdfStructure(int DistinctImages, IReadOnlyList<FontUse> Fonts);

/// <summary>
/// Fonts and images actually used by page content: fonts selected with Tf and images painted with Do, following form XObjects.
/// Resources that are declared but never used are ignored.
/// </summary>
internal static class PdfSharpStructure
{
    public static PdfStructure Inspect(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf, writable: false);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        var state = new State();
        foreach (var page in document.Pages)
        {
            var resources = page.Elements.GetDictionary("/Resources") ?? InheritedResources(page);
            Scan(ContentReader.ReadContent(page), resources, state, depth: 0);
        }
        return new PdfStructure(state.Images.Count, state.Fonts.Values.ToArray());
    }

    private sealed class State
    {
        public HashSet<object> Images { get; } = new(ReferenceEqualityComparer.Instance);

        public Dictionary<object, FontUse> Fonts { get; } = new(ReferenceEqualityComparer.Instance);

        public HashSet<object> Forms { get; } = new(ReferenceEqualityComparer.Instance);
    }

    private static PdfDictionary? InheritedResources(PdfDictionary page)
    {
        for (var parent = page.Elements.GetDictionary("/Parent"); parent is not null; parent = parent.Elements.GetDictionary("/Parent"))
            if (parent.Elements.GetDictionary("/Resources") is { } resources)
                return resources;
        return null;
    }

    private static void Scan(CSequence sequence, PdfDictionary? resources, State state, int depth)
    {
        foreach (var item in sequence)
        {
            if (item is CSequence nested)
            {
                Scan(nested, resources, state, depth);
                continue;
            }
            if (item is not COperator op || op.Operands.Count == 0 || op.Operands[0] is not CName name || resources is null)
                continue;
            var key = name.Name.StartsWith('/') ? name.Name : "/" + name.Name;
            if (op.Name == "Tf" && resources.Elements.GetDictionary("/Font")?.Elements.GetDictionary(key) is { } font)
            {
                if (!state.Fonts.ContainsKey(font))
                    state.Fonts[font] = new FontUse(font.Elements.GetName("/BaseFont") is { Length: > 0 } n ? n : key, IsEmbedded(font));
            }
            else if (op.Name == "Do" && resources.Elements.GetDictionary("/XObject")?.Elements.GetDictionary(key) is { } xobject)
            {
                var subtype = xobject.Elements.GetName("/Subtype");
                if (subtype == "/Image")
                    state.Images.Add(xobject);
                else if (subtype == "/Form" && depth < 16 && state.Forms.Add(xobject) && xobject.Stream?.UnfilteredValue is { } formContent)
                    Scan(ContentReader.ReadContent(formContent), xobject.Elements.GetDictionary("/Resources") ?? resources, state, depth + 1);
            }
        }
    }

    private static bool IsEmbedded(PdfDictionary font)
    {
        var subtype = font.Elements.GetName("/Subtype");
        if (subtype == "/Type3")
            return true;
        var descriptor = subtype == "/Type0"
            ? (font.Elements.GetArray("/DescendantFonts")?.Elements.GetDictionary(0))?.Elements.GetDictionary("/FontDescriptor")
            : font.Elements.GetDictionary("/FontDescriptor");
        return descriptor is not null && (descriptor.Elements.ContainsKey("/FontFile") ||
                                          descriptor.Elements.ContainsKey("/FontFile2") ||
                                          descriptor.Elements.ContainsKey("/FontFile3"));
    }
}

internal static class PdfPigStructure
{
    public static PdfStructure Inspect(byte[] pdf)
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdf);
        var placements = 0;
        var fonts = new Dictionary<string, FontUse>();
        foreach (var page in document.GetPages())
        {
            placements += page.GetImages().Count();
            foreach (var letter in page.Letters)
            {
                var name = letter.FontName ?? "unknown";
                if (!fonts.ContainsKey(name))
                    fonts[name] = new FontUse(name, !IsStandard14(name));
            }
        }
        return new PdfStructure(placements, fonts.Values.ToArray());
    }

    private static bool IsStandard14(string? name) => name is not null && !name.Contains('+') &&
        (name.StartsWith("Helvetica", StringComparison.Ordinal) || name.StartsWith("Times", StringComparison.Ordinal) ||
         name.StartsWith("Courier", StringComparison.Ordinal) || name is "Symbol" or "ZapfDingbats");
}
