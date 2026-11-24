using SkiaSharp;
using UnlimitedPDF.Benchmarks.Core.Imaging;

namespace UnlimitedPDF.Benchmarks.Core.Fixtures;

public enum ImageFormat
{
    Png,
    Jpeg,
}

/// <summary>A deterministic synthetic raster image with its encoded bytes.</summary>
public sealed record FixtureImage(int Index, int PixelWidth, int PixelHeight, ImageFormat Format, byte[] Encoded, byte[] Rgb);

public static class ImageFixtures
{
    public const int DefaultWidth = 320;
    public const int DefaultHeight = 240;
    public const int JpegQuality = 85;

    /// <summary>Photo-like pixels: a seeded two-colour gradient, soft noise and a few discs. Strongly coloured so renders can be checked.</summary>
    public static byte[] Pixels(int seed, int width = DefaultWidth, int height = DefaultHeight)
    {
        var random = new Random(5000 + seed);
        var (r0, g0, b0) = ((byte)random.Next(40, 230), (byte)random.Next(40, 230), (byte)random.Next(40, 230));
        var (r1, g1, b1) = ((byte)random.Next(40, 230), (byte)random.Next(40, 230), (byte)random.Next(40, 230));
        var discs = Enumerable.Range(0, 5)
            .Select(_ => (X: random.Next(width), Y: random.Next(height), R: random.Next(12, 48),
                C: ((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256))))
            .ToArray();
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var t = (x + y) / (double)(width + height);
                var noise = random.Next(-12, 13);
                var r = r0 + (r1 - r0) * t + noise;
                var g = g0 + (g1 - g0) * t + noise;
                var b = b0 + (b1 - b0) * t + noise;
                foreach (var disc in discs)
                {
                    var dx = x - disc.X;
                    var dy = y - disc.Y;
                    if (dx * dx + dy * dy <= disc.R * disc.R)
                        (r, g, b) = (disc.C.Item1, disc.C.Item2, disc.C.Item3);
                }
                var i = (y * width + x) * 3;
                rgb[i] = (byte)Math.Clamp(r, 0, 255);
                rgb[i + 1] = (byte)Math.Clamp(g, 0, 255);
                rgb[i + 2] = (byte)Math.Clamp(b, 0, 255);
            }
        }
        return rgb;
    }

    public static FixtureImage Create(int index, ImageFormat format, int width = DefaultWidth, int height = DefaultHeight)
    {
        var rgb = Pixels(index, width, height);
        var encoded = format == ImageFormat.Png ? PngEncoder.EncodeRgb(width, height, rgb) : EncodeJpeg(width, height, rgb);
        return new FixtureImage(index, width, height, format, encoded, rgb);
    }

    public static IReadOnlyList<FixtureImage> CreateSet(int count, ImageFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return Enumerable.Range(0, count).Select(i => Create(i, format)).ToArray();
    }

    private static byte[] EncodeJpeg(int width, int height, byte[] rgb)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var bitmap = new SKBitmap(info);
        var rgba = new byte[width * height * 4];
        for (int s = 0, d = 0; s < rgb.Length; s += 3, d += 4)
        {
            rgba[d] = rgb[s];
            rgba[d + 1] = rgb[s + 1];
            rgba[d + 2] = rgb[s + 2];
            rgba[d + 3] = 255;
        }
        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, bitmap.GetPixels(), rgba.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return data.ToArray();
    }
}

/// <summary>N distinct images, four per A4 page in a 2 × 2 grid of 240 × 180 pt boxes.</summary>
public sealed class ImageDocument
{
    public const int ImagesPerPage = 4;
    public const double BoxWidth = 240;
    public const double BoxHeight = 180;

    private ImageDocument(IReadOnlyList<FixtureImage> images) => Images = images;

    public IReadOnlyList<FixtureImage> Images { get; }

    public int PageCount => (Images.Count + ImagesPerPage - 1) / ImagesPerPage;

    public static string Caption(int index) => $"Image {index + 1:D4}";

    public static ImageDocument Create(int count, ImageFormat format) => new(ImageFixtures.CreateSet(count, format));

    /// <summary>Bottom-left corner of image <paramref name="index"/> on its page, plus the caption baseline.</summary>
    public static (int Page, double X, double Y, double CaptionY) Placement(int index)
    {
        var page = index / ImagesPerPage;
        var slot = index % ImagesPerPage;
        var x = PageGeometry.Margin + slot % 2 * (BoxWidth + 15);
        var y = 790 - BoxHeight - slot / 2 * (BoxHeight + 60);
        return (page, x, y, y - 16);
    }
}
