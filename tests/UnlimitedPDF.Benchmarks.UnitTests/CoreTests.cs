using SkiaSharp;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using UnlimitedPDF.Benchmarks.Core.Imaging;
using UnlimitedPDF.Benchmarks.Core.Profiles;
using UnlimitedPDF.Benchmarks.Core.Provenance;

namespace UnlimitedPDF.Benchmarks.UnitTests;

public class StatisticsTests
{
    [Fact]
    public void PercentileInterpolatesBetweenRanks()
    {
        double[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.Equal(5.5, Statistics.Percentile(values, 50), 10);
        Assert.Equal(9.55, Statistics.Percentile(values, 95), 10);
        Assert.Equal(1, Statistics.Percentile(values, 0));
        Assert.Equal(10, Statistics.Percentile(values, 100));
        Assert.Equal(42, Statistics.Percentile([42.0], 99));
    }

    [Fact]
    public void PercentileRejectsInvalidInput()
    {
        Assert.Throws<ArgumentException>(() => Statistics.Percentile([], 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => Statistics.Percentile([1.0], 101));
    }

    [Theory]
    [InlineData(200.0, 100.0, 2.0)]
    [InlineData(50.0, 100.0, 0.5)]
    public void RatioIsCompetitorOverBaseline(double competitor, double baseline, double expected) =>
        Assert.Equal(expected, Statistics.Ratio(competitor, baseline));

    [Theory]
    [InlineData(100.0, 0.0)]
    [InlineData(100.0, -1.0)]
    [InlineData(100.0, double.NaN)]
    [InlineData(double.PositiveInfinity, 10.0)]
    public void RatioIsNotCalculatedForInvalidBaselines(double competitor, double baseline) =>
        Assert.Null(Statistics.Ratio(competitor, baseline));

    [Fact]
    public void RatioIsNotCalculatedWhenAValueIsMissing()
    {
        Assert.Null(Statistics.Ratio(null, 10));
        Assert.Null(Statistics.Ratio(10, null));
    }
}

public class FixtureTests
{
    [Fact]
    public void TextFixturesAreDeterministic()
    {
        var a = TextDocument.Create(3);
        var b = TextDocument.Create(3);
        Assert.Equal(a.Pages.SelectMany(p => p).Select(l => l.Text), b.Pages.SelectMany(p => p).Select(l => l.Text));
        Assert.Equal(3, a.PageCount);
        Assert.All(a.Pages, p => Assert.Equal(TextDocument.LinesPerPage, p.Count));
        Assert.StartsWith(TextDocument.LineId(2, 1), a.Pages[1][0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TextLinesStayInsideTheTextArea()
    {
        var lines = TextDocument.Create(1).Pages[0];
        Assert.All(lines, l => Assert.InRange(l.Y, PageGeometry.Margin, PageGeometry.Height - PageGeometry.Margin));
        Assert.All(RichTextDocument.Create(1, true).Pages[0], l => Assert.InRange(l.Y, PageGeometry.Margin, PageGeometry.Height - PageGeometry.Margin));
    }

    [Fact]
    public void FixtureChecksumIsStable() => Assert.Equal(FixtureManifest.Sha256(), FixtureManifest.Sha256());

    [Fact]
    public void FontsAreTheExpectedTrueTypeFiles()
    {
        foreach (var face in FontAssets.AllFaces)
        {
            var bytes = FontAssets.Bytes(face);
            Assert.True(bytes.Length > 10_000, face.ToString());
            Assert.Equal([0, 1, 0, 0], bytes[..4]);
        }
    }

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.Jpeg)]
    public void ImagesDecodeToTheirDeclaredSize(ImageFormat format)
    {
        var image = ImageFixtures.Create(7, format);
        using var bitmap = SKBitmap.Decode(image.Encoded);
        Assert.NotNull(bitmap);
        Assert.Equal(ImageFixtures.DefaultWidth, bitmap.Width);
        Assert.Equal(ImageFixtures.DefaultHeight, bitmap.Height);
    }

    [Fact]
    public void PngEncoderRoundTripsPixelsExactly()
    {
        var rgb = ImageFixtures.Pixels(3, 16, 8);
        using var bitmap = SKBitmap.Decode(PngEncoder.EncodeRgb(16, 8, rgb));
        Assert.NotNull(bitmap);
        var pixel = bitmap.GetPixel(5, 4);
        var i = (4 * 16 + 5) * 3;
        Assert.Equal((rgb[i], rgb[i + 1], rgb[i + 2]), (pixel.Red, pixel.Green, pixel.Blue));
    }

    [Fact]
    public void ImageDocumentPlacesFourImagesPerPageInsideMargins()
    {
        var document = ImageDocument.Create(9, ImageFormat.Png);
        Assert.Equal(3, document.PageCount);
        for (var i = 0; i < 9; i++)
        {
            var (page, x, y, captionY) = ImageDocument.Placement(i);
            Assert.Equal(i / 4, page);
            Assert.InRange(x + ImageDocument.BoxWidth, 0, PageGeometry.Width - PageGeometry.Margin);
            Assert.InRange(captionY, PageGeometry.Margin, y);
        }
    }

    [Fact]
    public void PageSelectionsExpandRangesInOrder()
    {
        var selection = PageSelection.FromRanges((2, 3), (7, 5));
        Assert.Equal([2, 3, 7, 6, 5], selection.Pages);
        Assert.Equal([1, 4, 8], selection.Complement(8));
        Assert.Equal(150, ManipulationFixtures.ExtractSelection(DocumentSize.Large).Pages.Count);
        Assert.Equal(Enumerable.Range(1, 10).Reverse(), ManipulationFixtures.ReverseSelection(DocumentSize.Small).Pages);
    }

    [Theory]
    [InlineData(BusinessDocumentKind.Invoice)]
    [InlineData(BusinessDocumentKind.BankStatement)]
    [InlineData(BusinessDocumentKind.ProductCatalog)]
    public void BusinessDocumentTablesHaveConsistentColumns(BusinessDocumentKind kind)
    {
        var document = BusinessDocuments.Create(kind);
        Assert.All(document.Tables, t =>
        {
            Assert.Equal(t.Headers.Count, t.ColumnWidths.Count);
            Assert.Equal(t.Headers.Count, t.Alignments.Count);
            Assert.All(t.Rows, r => Assert.Equal(t.Headers.Count, r.Cells.Count));
            Assert.InRange(t.Width, 1, PageGeometry.ContentWidth);
        });
        Assert.NotEmpty(document.RequiredText());
    }

    [Fact]
    public void MixedLanguageLinesUseEveryScriptFont()
    {
        var faces = MultilingualDocument.Create(Language.Mixed, 1).FacesUsed.ToHashSet();
        Assert.Contains(FontFace.Devanagari, faces);
        Assert.Contains(FontFace.Telugu, faces);
        Assert.Contains(FontFace.Tamil, faces);
        Assert.Contains(FontFace.Regular, faces);
    }

    [Fact]
    public void HtmlFixturesAreSelfContained()
    {
        foreach (var kind in Enum.GetValues<HtmlKind>())
        {
            var html = HtmlDocument.Create(kind, HtmlSize.Small).Html;
            Assert.DoesNotContain("http://", html, StringComparison.Ordinal);
            Assert.DoesNotContain("https://", html, StringComparison.Ordinal);
            Assert.DoesNotContain("display:flex", html, StringComparison.Ordinal);
        }
        Assert.Equal(2, HtmlDocument.Create(HtmlKind.TablesCssImages, HtmlSize.Small).ImageCount);
    }
}

public class ProfileTests
{
    [Fact]
    public void ProfilesParseCaseInsensitively()
    {
        Assert.Equal(ProfileName.Full, BenchmarkProfile.Parse("FULL").Name);
        Assert.Throws<ArgumentException>(() => BenchmarkProfile.Parse("huge"));
    }

    [Fact]
    public void StressOnlyAddsSizesBeyondFull()
    {
        var full = BenchmarkProfile.Full;
        var stress = BenchmarkProfile.Stress;
        Assert.All(stress.TableRows, r => Assert.True(r > full.TableRows.Max()));
        Assert.All(stress.TextPages, p => Assert.True(p > full.TextPages.Max()));
        Assert.All(stress.MergeDocuments, d => Assert.True(d > full.MergeDocuments.Max()));
        Assert.Contains(100_000, stress.TableRows);
    }

    [Fact]
    public void PlannedSizesAreCovered()
    {
        var full = BenchmarkProfile.Full;
        Assert.Equal([1, 10, 50, 100, 500, 1000], full.TextPages);
        Assert.Equal([10, 100, 1000], full.TableRows);
        Assert.Equal([2, 10, 50, 100], full.MergeDocuments);
        Assert.Equal([10, 100, 1000], full.SplitPages);
        Assert.Equal([1, 2, 4, 8, 16], full.Profiler.ConcurrencyLevels);
        Assert.Equal([100, 1000, 10000], full.Profiler.SequentialCounts);
        Assert.Equal(Enum.GetValues<Language>(), full.Languages);
    }
}
