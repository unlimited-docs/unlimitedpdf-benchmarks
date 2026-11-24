using SkiaSharp;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using UnlimitedPDF.Benchmarks.Core.Profiles;

namespace UnlimitedPDF.Benchmarks.IntegrationTests;

/// <summary>
/// Measures every positioned fixture line with the real Noto Sans advance widths and requires it to end inside the right
/// margin. A line that runs off the page is drawn differently by different libraries (some keep off-page glyphs in the
/// content stream, Skia drops them), which would turn a fixture defect into a validation failure for one library.
/// </summary>
public class FixtureGeometryTests
{
    private const double RightEdge = PageGeometry.Width - PageGeometry.Margin;

    public static TheoryData<string, int> PositionedFixtures()
    {
        var data = new TheoryData<string, int>();
        var profiles = BenchmarkProfile.All;
        foreach (var pages in profiles.SelectMany(p => p.TextPages.Concat(p.StandardFontPages).Concat(p.FileOutputPages)).Append(1).Distinct())
            data.Add("Text", pages);
        foreach (var pages in profiles.SelectMany(p => p.RichTextPages).Distinct())
            data.Add("RichText", pages);
        foreach (var pages in profiles.SelectMany(p => p.MixedPages).Distinct())
            data.Add("Mixed", pages);
        return data;
    }

    [Theory]
    [MemberData(nameof(PositionedFixtures))]
    public void Lines_end_inside_the_right_margin(string fixture, int pages)
    {
        var lines = fixture switch
        {
            "Text" => TextDocument.Create(pages).Pages,
            "RichText" => RichTextDocument.Create(pages, colored: false).Pages,
            _ => MixedDocument.Create(pages).Text,
        };

        var fonts = new Dictionary<FontFace, SKFont>();
        try
        {
            var widest = 0.0;
            string? overflow = null;
            for (var p = 0; p < lines.Count; p++)
            {
                foreach (var line in lines[p])
                {
                    if (!fonts.TryGetValue(line.Face, out var font))
                        fonts[line.Face] = font = new SKFont(SKTypeface.FromFile(FontAssets.PathOf(line.Face)), 1000);
                    var right = line.X + font.MeasureText(line.Text) / 1000 * line.FontSize;
                    if (right > widest)
                        widest = right;
                    if (right > RightEdge && overflow is null)
                        overflow = $"page {p + 1}: \"{line.Text}\" ends at {right:0.0} pt";
                }
            }

            Assert.True(overflow is null, $"{overflow}; right margin is at {RightEdge} pt");
            Assert.True(widest > RightEdge * 0.6, $"Widest line ends at {widest:0.0} pt; the fixture should still use most of the width.");
        }
        finally
        {
            foreach (var font in fonts.Values)
            {
                font.Typeface.Dispose();
                font.Dispose();
            }
        }
    }
}
