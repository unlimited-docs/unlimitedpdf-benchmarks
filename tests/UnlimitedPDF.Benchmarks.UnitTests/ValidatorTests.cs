using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using UnlimitedPDF.Benchmarks.Core.Validation;

namespace UnlimitedPDF.Benchmarks.UnitTests;

/// <summary>The validator must accept correct output and reject each kind of wrong output. Inputs come from the independent fixture writer.</summary>
public class ValidatorTests
{
    private static readonly byte[] Mixed = FixturePdfWriter.Create(1, 3, FixtureContent.Mixed);
    private static readonly byte[] Text = FixturePdfWriter.Create(2, 2, FixtureContent.Text);

    private static PdfExpectation Expect(int pages, int document = 1) => new()
    {
        Producer = "Fixture",
        PageCount = pages,
        PageText = p => [FixturePdfWriter.Marker(document, p)],
    };

    [Fact]
    public void FixtureWriterOutputPassesAndIsReadableByEveryParser()
    {
        var report = PdfValidator.Validate(Mixed, Expect(3));
        Assert.True(report.Passed, string.Join("; ", report.Failures));
        Assert.Equal(3, report.PageCount);
        using var pig = UglyToad.PdfPig.PdfDocument.Open(Mixed);
        Assert.Equal(3, pig.NumberOfPages);
        using var sharp = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(Mixed), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.Equal(3, sharp.PageCount);
    }

    [Fact]
    public void FixtureWriterIsDeterministic() => Assert.Equal(FixturePdfWriter.Create(1, 3, FixtureContent.Mixed), Mixed);

    [Fact]
    public void RejectsWrongPageCount()
    {
        var report = PdfValidator.Validate(Mixed, Expect(4));
        Assert.Equal(ResultStatus.FailedValidation, report.Status);
        Assert.Contains(report.Failures, f => f.Contains("Expected 4 pages", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsMissingOrReorderedText()
    {
        var report = PdfValidator.Validate(Mixed, new PdfExpectation
        {
            Producer = "Fixture",
            PageCount = 3,
            PageText = p => [FixturePdfWriter.Marker(1, 4 - p)],
        });
        Assert.False(report.Passed);
    }

    [Fact]
    public void RejectsMissingImages()
    {
        var report = PdfValidator.Validate(Text, Expect(2, document: 2));
        Assert.True(report.Passed, string.Join("; ", report.Failures));
        var withImages = new PdfExpectation { Producer = "Fixture", PageCount = 2, MinDistinctImages = 1 };
        Assert.False(PdfValidator.Validate(Text, withImages).Passed);
        Assert.True(PdfValidator.Validate(Mixed, new PdfExpectation { Producer = "Fixture", MinDistinctImages = 3 }).Passed);
    }

    [Fact]
    public void RejectsNonEmbeddedFontsWhenEmbeddingIsRequired()
    {
        // The fixture writer uses the non-embedded standard Helvetica.
        Assert.False(PdfValidator.Validate(Text, new PdfExpectation { Producer = "Fixture", Fonts = FontExpectation.AllEmbedded }).Passed);
        Assert.True(PdfValidator.Validate(Text, new PdfExpectation { Producer = "Fixture", Fonts = FontExpectation.StandardFontUsed }).Passed);
    }

    [Fact]
    public void RejectsColourlessOutputWhenColourIsExpected() =>
        Assert.False(PdfValidator.Validate(Text, new PdfExpectation { Producer = "Fixture", ExpectColor = true }).Passed);

    [Fact]
    public void RejectsNonPdfAndTruncatedOutput()
    {
        Assert.False(PdfValidator.Validate("not a pdf"u8.ToArray(), Expect(1)).Passed);
        Assert.False(PdfValidator.Validate(Mixed[..(Mixed.Length / 3)], Expect(3)).Passed);
    }

    [Fact]
    public void RejectsWrongPageSize() =>
        Assert.False(PdfValidator.Validate(Mixed, new PdfExpectation { Producer = "Fixture", PageSize = (612, 792) }).Passed);

    [Fact]
    public void CountsTokensAcrossPages()
    {
        var expectation = new PdfExpectation
        {
            Producer = "Fixture",
            CountedToken = new System.Text.RegularExpressions.Regex(@"Fixture D001 P\d{4}"),
            TokenCount = 3,
        };
        Assert.True(PdfValidator.Validate(Mixed, expectation).Passed);
        Assert.False(PdfValidator.Validate(Mixed, new PdfExpectation { Producer = "Fixture", CountedToken = expectation.CountedToken, TokenCount = 4 }).Passed);
    }

    [Fact]
    public void NormalizationIgnoresWhitespaceButNotCharacters()
    {
        Assert.Equal("ab", PdfValidator.Normalize(" a \r\n b "));
        Assert.NotEqual(PdfValidator.Normalize("a?b"), PdfValidator.Normalize("ab"));
    }

    [Fact]
    public void WritesAThumbnail()
    {
        var path = Path.Combine(Path.GetTempPath(), $"updf-thumb-{Guid.NewGuid():N}.png");
        try
        {
            Assert.True(PdfValidator.Validate(Mixed, Expect(3), path).Passed);
            Assert.True(new FileInfo(path).Length > 1000);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
