using System.Text.RegularExpressions;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using UnlimitedPDF.Benchmarks.Core.Validation;

namespace UnlimitedPDF.Benchmarks.Runner.Infrastructure;

/// <summary>Expected output for each fixture, shared by the validation pass and the integration tests.</summary>
public static partial class Expectations
{
    [GeneratedRegex(@"R\d{6}")]
    public static partial Regex RowIdPattern();

    public static PdfExpectation Text(string producer, TextDocument document, FontExpectation fonts = FontExpectation.AllEmbedded) => new()
    {
        Producer = producer,
        PageCount = document.PageCount,
        PageText = page => document.Pages[page - 1].Select(l => l.Text.Length > 12 ? l.Text[..12] : l.Text)
            .Append(document.Pages[page - 1][0].Text),
        Fonts = fonts,
    };

    public static PdfExpectation RichText(string producer, RichTextDocument document) => new()
    {
        Producer = producer,
        PageCount = document.PageCount,
        PageText = page => document.Pages[page - 1].Select(l => l.Text),
        Fonts = FontExpectation.AllEmbedded,
        ExpectColor = document.Colored,
    };

    public static PdfExpectation Images(string producer, ImageDocument document) => new()
    {
        Producer = producer,
        PageCount = document.PageCount,
        PageText = page => Enumerable.Range((page - 1) * ImageDocument.ImagesPerPage,
                Math.Min(ImageDocument.ImagesPerPage, document.Images.Count - (page - 1) * ImageDocument.ImagesPerPage))
            .Select(ImageDocument.Caption),
        MinDistinctImages = document.Images.Count,
        ExpectColor = true,
    };

    public static PdfExpectation Vector(string producer, VectorDocument document) => new()
    {
        Producer = producer,
        PageCount = document.PageCount,
        PageText = page => [VectorDocument.PageLabel(page)],
        ExpectColor = true,
    };

    public static PdfExpectation Mixed(string producer, MixedDocument document) => new()
    {
        Producer = producer,
        PageCount = document.PageCount,
        PageText = page => document.Text[page - 1].Select(l => l.Text),
        MinDistinctImages = document.PageCount,
        Fonts = FontExpectation.AllEmbedded,
        ExpectColor = true,
    };

    public static PdfExpectation Table(string producer, TableSpec table) => new()
    {
        Producer = producer,
        PageText = table.RepeatHeader ? _ => table.Headers : null,
        DocumentText = table.Rows.Take(3).Concat(table.Rows.TakeLast(3)).SelectMany(r => r.Cells),
        CountedToken = RowIdPattern(),
        TokenCount = table.Rows.Count,
        MinDistinctImages = table.Rows.Count(r => r.Image is not null),
        Fonts = FontExpectation.AllEmbedded,
    };

    public static PdfExpectation HeaderFooter(string producer, HeaderFooterDocument document) => new()
    {
        Producer = producer,
        PageCount = document.PageCount,
        PageText = page => document.Pages[page - 1].Select(l => l[..12])
            .Append(HeaderFooterDocument.HeaderText)
            .Append(HeaderFooterDocument.FooterText(page, document.PageCount)),
        Fonts = FontExpectation.AllEmbedded,
    };

    public static PdfExpectation Business(string producer, BusinessDocument document) => new()
    {
        Producer = producer,
        PageSize = (PageGeometry.Width, PageGeometry.Height),
        DocumentText = document.RequiredText().Distinct().ToArray(),
        MinDistinctImages = document.ImageCount,
        Fonts = FontExpectation.AllEmbedded,
        ExpectColor = document.ImageCount > 0,
    };

    public static PdfExpectation Multilingual(string producer, MultilingualDocument document) => new()
    {
        Producer = producer,
        PageCount = document.PageCount,
        PageText = page => document.Pages[page - 1].Select(l => l.Text),
        Fonts = FontExpectation.AllEmbedded,
    };

    /// <summary>Pages of fixture document <paramref name="document"/>, in the given order.</summary>
    public static PdfExpectation FixturePages(string producer, int document, IReadOnlyList<int> pages, FixtureContent content) => new()
    {
        Producer = producer,
        PageCount = pages.Count,
        PageText = page => [FixturePdfWriter.Marker(document, pages[page - 1])],
        MinDistinctImages = content == FixtureContent.Text ? 0 : pages.Distinct().Count(),
        ExpectColor = content != FixtureContent.Text,
    };

    public static PdfExpectation Merged(string producer, int documents, FixtureContent content) => new()
    {
        Producer = producer,
        PageCount = documents * ManipulationFixtures.MergePagesPerDocument,
        PageText = page => [FixturePdfWriter.Marker((page - 1) / ManipulationFixtures.MergePagesPerDocument + 1,
            (page - 1) % ManipulationFixtures.MergePagesPerDocument + 1)],
        MinDistinctImages = content == FixtureContent.Text ? 0 : documents * ManipulationFixtures.MergePagesPerDocument,
        ExpectColor = content != FixtureContent.Text,
    };

    public static PdfExpectation Html(string producer, HtmlDocument html) => new()
    {
        Producer = producer,
        PageSize = (PageGeometry.Width, PageGeometry.Height),
        DocumentText = html.RequiredText,
        MinDistinctImages = html.ImageCount,
        Fonts = FontExpectation.AllEmbedded,
        ExpectColor = html.Kind == HtmlKind.TablesCssImages,
    };
}
