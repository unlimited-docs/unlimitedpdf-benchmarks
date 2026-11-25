using System.Text;
using Language = UnlimitedPDF.Benchmarks.Core.Fixtures.Language;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;

namespace UnlimitedPDF.Benchmarks.Adapters.SkiaSharp;

/// <summary>
/// SkiaSharp 4.157.0 (MIT) SKDocument PDF backend with SKCanvas drawing. Typefaces, fonts and HarfBuzz shapers are created once
/// and reused. Complex scripts are shaped with SkiaSharp.HarfBuzz and drawn as text runs that carry the UTF-8 text and clusters,
/// which is how Skia makes shaped text extractable. SkiaSharp has no table, flowing layout or page-manipulation API.
/// One instance per thread.
/// </summary>
public sealed class SkiaSharpAdapter :
    IEmptyDocumentGenerator, ITextDocumentGenerator, IRichTextGenerator, IImageDocumentGenerator, IVectorGraphicsGenerator, IMixedContentGenerator,
    IMultilingualGenerator, IDisposable
{
    private const float PageWidth = (float)PageGeometry.Width;
    private const float PageHeight = (float)PageGeometry.Height;
    private readonly Dictionary<FontFace, SKTypeface> _typefaces;
    private readonly Dictionary<FontFace, SKShaper> _shapers;
    private readonly Dictionary<(FontFace Face, double Size), SKFont> _fonts = [];
    private readonly SKPaint _black = new() { Color = SKColors.Black, IsAntialias = true };

    public SkiaSharpAdapter()
    {
        _typefaces = FontAssets.AllFaces.ToDictionary(f => f, f => SKTypeface.FromData(SKData.CreateCopy(FontAssets.Bytes(f))));
        _shapers = _typefaces.ToDictionary(p => p.Key, p => new SKShaper(p.Value));
    }

    public string LibraryId => LibraryIds.SkiaSharp;

    public bool SupportsTextColor => true;

    public byte[] CreateEmptyDocument(int pages)
    {
        using var stream = new SKDynamicMemoryWStream();
        using (var pdf = SKDocument.CreatePdf(stream))
        {
            for (var i = 0; i < pages; i++)
            {
                pdf.BeginPage(PageWidth, PageHeight);
                pdf.EndPage();
            }
            pdf.Close();
        }
        return Detach(stream);
    }

    public byte[] CreateTextDocument(TextDocument document)
    {
        using var stream = new SKDynamicMemoryWStream();
        using (var pdf = SKDocument.CreatePdf(stream))
        {
            WriteText(pdf, document);
            pdf.Close();
        }
        return Detach(stream);
    }

    public void SaveTextDocument(TextDocument document, string path)
    {
        using var pdf = SKDocument.CreatePdf(path);
        WriteText(pdf, document);
        pdf.Close();
    }

    private void WriteText(SKDocument pdf, TextDocument document)
    {
        foreach (var lines in document.Pages)
        {
            var canvas = pdf.BeginPage(PageWidth, PageHeight);
            foreach (var line in lines)
                canvas.DrawText(line.Text, (float)line.X, PageHeight - (float)line.Y, SKTextAlign.Left, Font(line.Face, line.FontSize), _black);
            pdf.EndPage();
        }
    }

    public byte[] CreateRichTextDocument(RichTextDocument document)
    {
        var paints = new Dictionary<RgbColor, SKPaint>();
        try
        {
            using var stream = new SKDynamicMemoryWStream();
            using (var pdf = SKDocument.CreatePdf(stream))
            {
                foreach (var lines in document.Pages)
                {
                    var canvas = pdf.BeginPage(PageWidth, PageHeight);
                    foreach (var line in lines)
                    {
                        var paint = _black;
                        if (line.Color is { } c && !paints.TryGetValue(c, out paint!))
                            paints[c] = paint = new SKPaint { Color = new SKColor(c.R, c.G, c.B), IsAntialias = true };
                        canvas.DrawText(line.Text, (float)line.X, PageHeight - (float)line.Y, SKTextAlign.Left, Font(line.Face, line.FontSize), paint);
                    }
                    pdf.EndPage();
                }
                pdf.Close();
            }
            return Detach(stream);
        }
        finally
        {
            foreach (var paint in paints.Values)
                paint.Dispose();
        }
    }

    public byte[] CreateImageDocument(ImageDocument document)
    {
        var images = new List<SKImage>(document.Images.Count);
        try
        {
            using var stream = new SKDynamicMemoryWStream();
            using (var pdf = SKDocument.CreatePdf(stream))
            {
                SKCanvas? canvas = null;
                var caption = Font(FontFace.Regular, 10);
                for (var i = 0; i < document.Images.Count; i++)
                {
                    if (i % ImageDocument.ImagesPerPage == 0)
                    {
                        if (canvas is not null)
                            pdf.EndPage();
                        canvas = pdf.BeginPage(PageWidth, PageHeight);
                    }
                    var (_, x, y, captionY) = ImageDocument.Placement(i);
                    var image = Decode(document.Images[i]);
                    images.Add(image);
                    var top = PageHeight - (float)(y + ImageDocument.BoxHeight);
                    canvas!.DrawImage(image, SKRect.Create((float)x, top, (float)ImageDocument.BoxWidth, (float)ImageDocument.BoxHeight),
                        new SKSamplingOptions(SKFilterMode.Linear), null);
                    canvas.DrawText(ImageDocument.Caption(i), (float)x, PageHeight - (float)captionY, SKTextAlign.Left, caption, _black);
                }
                pdf.EndPage();
                pdf.Close();
            }
            return Detach(stream);
        }
        finally
        {
            foreach (var image in images)
                image.Dispose();
        }
    }

    public byte[] CreateVectorDocument(VectorDocument document)
    {
        using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, IsAntialias = true };
        using var fill = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var stream = new SKDynamicMemoryWStream();
        using (var pdf = SKDocument.CreatePdf(stream))
        {
            for (var p = 0; p < document.PageCount; p++)
            {
                var canvas = pdf.BeginPage(PageWidth, PageHeight);
                foreach (var s in document.Pages[p])
                {
                    var color = new SKColor(s.Color.R, s.Color.G, s.Color.B);
                    var x = (float)s.X;
                    var y = PageHeight - (float)s.Y;
                    switch (s.Kind)
                    {
                        case ShapeKind.Line:
                            stroke.Color = color;
                            stroke.StrokeWidth = (float)s.LineWidth;
                            canvas.DrawLine(x, y, x + (float)s.Width, y - (float)s.Height, stroke);
                            break;
                        case ShapeKind.StrokedRectangle:
                            stroke.Color = color;
                            stroke.StrokeWidth = (float)s.LineWidth;
                            canvas.DrawRect(x, y - (float)s.Height, (float)s.Width, (float)s.Height, stroke);
                            break;
                        case ShapeKind.FilledRectangle:
                            fill.Color = color;
                            canvas.DrawRect(x, y - (float)s.Height, (float)s.Width, (float)s.Height, fill);
                            break;
                        case ShapeKind.Circle:
                            stroke.Color = color;
                            stroke.StrokeWidth = (float)s.LineWidth;
                            canvas.DrawCircle(x, y, (float)s.Width / 2, stroke);
                            break;
                    }
                }
                canvas.DrawText(VectorDocument.PageLabel(p + 1), 50, 60, SKTextAlign.Left, Font(FontFace.Regular, 10), _black);
                pdf.EndPage();
            }
            pdf.Close();
        }
        return Detach(stream);
    }

    public byte[] CreateMixedDocument(MixedDocument document)
    {
        var images = new List<SKImage>(document.PageCount);
        try
        {
            using var fill = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
            using var stream = new SKDynamicMemoryWStream();
            using (var pdf = SKDocument.CreatePdf(stream))
            {
                for (var p = 0; p < document.PageCount; p++)
                {
                    var canvas = pdf.BeginPage(PageWidth, PageHeight);
                    foreach (var line in document.Text[p])
                        canvas.DrawText(line.Text, (float)line.X, PageHeight - (float)line.Y, SKTextAlign.Left, Font(line.Face, line.FontSize), _black);
                    var image = SKImage.FromEncodedData(document.Images[p].Encoded);
                    images.Add(image);
                    canvas.DrawImage(image, SKRect.Create((float)MixedDocument.ImageX,
                            PageHeight - (float)(MixedDocument.ImageY + MixedDocument.ImageHeight),
                            (float)MixedDocument.ImageWidth, (float)MixedDocument.ImageHeight),
                        new SKSamplingOptions(SKFilterMode.Linear), null);
                    foreach (var r in document.Rectangles[p])
                    {
                        fill.Color = new SKColor(r.Color.R, r.Color.G, r.Color.B);
                        canvas.DrawRect((float)r.X, PageHeight - (float)(r.Y + r.Height), (float)r.Width, (float)r.Height, fill);
                    }
                    pdf.EndPage();
                }
                pdf.Close();
            }
            return Detach(stream);
        }
        finally
        {
            foreach (var image in images)
                image.Dispose();
        }
    }

    public bool Supports(Language language) => true;

    public byte[] CreateMultilingualDocument(MultilingualDocument document)
    {
        using var stream = new SKDynamicMemoryWStream();
        using (var pdf = SKDocument.CreatePdf(stream))
        {
            foreach (var lines in document.Pages)
            {
                var canvas = pdf.BeginPage(PageWidth, PageHeight);
                foreach (var line in lines)
                {
                    var x = (float)line.X;
                    var y = PageHeight - (float)line.Y;
                    foreach (var run in line.Runs)
                        x += DrawShaped(canvas, run, x, y, Font(run.Face, line.FontSize));
                }
                pdf.EndPage();
            }
            pdf.Close();
        }
        return Detach(stream);
    }

    /// <summary>Shapes one run with HarfBuzz and draws it as a positioned text run carrying UTF-8 text and clusters.</summary>
    private float DrawShaped(SKCanvas canvas, TextRun run, float x, float y, SKFont font)
    {
        var utf8 = Encoding.UTF8.GetBytes(run.Text);
        using var buffer = new HarfBuzzSharp.Buffer();
        buffer.AddUtf8(utf8);
        buffer.GuessSegmentProperties();
        var shaped = _shapers[run.Face].Shape(buffer, font);
        var count = shaped.Codepoints.Length;
        if (count == 0)
            return 0;
        using var builder = new SKTextBlobBuilder();
        var glyphRun = builder.AllocateRawPositionedTextRun(font, count, utf8.Length, null);
        var glyphs = glyphRun.Glyphs;
        var positions = glyphRun.Positions;
        var clusters = glyphRun.Clusters;
        for (var i = 0; i < count; i++)
        {
            glyphs[i] = (ushort)shaped.Codepoints[i];
            positions[i] = shaped.Points[i];
            clusters[i] = shaped.Clusters[i];
        }
        utf8.CopyTo(glyphRun.Text);
        using var blob = builder.Build();
        if (blob is not null)
            canvas.DrawText(blob, x, y, _black);
        return shaped.Width;
    }

    public void Dispose()
    {
        foreach (var font in _fonts.Values)
            font.Dispose();
        foreach (var shaper in _shapers.Values)
            shaper.Dispose();
        foreach (var typeface in _typefaces.Values)
            typeface.Dispose();
        _black.Dispose();
    }

    private SKFont Font(FontFace face, double size)
    {
        if (!_fonts.TryGetValue((face, size), out var font))
            _fonts[(face, size)] = font = new SKFont(_typefaces[face], (float)size) { Subpixel = true };
        return font;
    }

    /// <summary>
    /// JPEGs stay lazily encoded so Skia can pass the JPEG data through. PNGs are decoded to a raster image first: drawing a lazily
    /// decoded PNG into a PDF canvas crashed SkiaSharp 4.157.0 natively (access violation) on Windows x64.
    /// </summary>
    private static SKImage Decode(FixtureImage fixture)
    {
        var image = SKImage.FromEncodedData(fixture.Encoded);
        if (fixture.Format != ImageFormat.Png)
            return image;
        var raster = image.ToRasterImage(ensurePixelData: true);
        image.Dispose();
        return raster;
    }

    private static byte[] Detach(SKDynamicMemoryWStream stream)
    {
        using var data = stream.DetachAsData();
        return data.ToArray();
    }
}
