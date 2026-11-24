using System.Security.Cryptography;

namespace UnlimitedPDF.Benchmarks.Core.Fixtures;

public enum FontFace
{
    Regular,
    Bold,
    Italic,
    Devanagari,
    Telugu,
    Tamil,
}

/// <summary>
/// The SIL OFL 1.1 Noto fonts in assets/fonts. Every library receives the same font bytes.
/// </summary>
public static class FontAssets
{
    public const string LatinFamily = "Noto Sans";
    public const string DevanagariFamily = "Noto Sans Devanagari";
    public const string TeluguFamily = "Noto Sans Telugu";
    public const string TamilFamily = "Noto Sans Tamil";

    private static readonly Lazy<string> Directory = new(FindDirectory);
    private static readonly Dictionary<FontFace, byte[]> Cache = [];
    private static readonly Lock CacheLock = new();

    public static string FontDirectory => Directory.Value;

    public static IReadOnlyList<FontFace> AllFaces { get; } = Enum.GetValues<FontFace>();

    public static string FileName(FontFace face) => face switch
    {
        FontFace.Regular => "NotoSans-Regular.ttf",
        FontFace.Bold => "NotoSans-Bold.ttf",
        FontFace.Italic => "NotoSans-Italic.ttf",
        FontFace.Devanagari => "NotoSansDevanagari-Regular.ttf",
        FontFace.Telugu => "NotoSansTelugu-Regular.ttf",
        FontFace.Tamil => "NotoSansTamil-Regular.ttf",
        _ => throw new ArgumentOutOfRangeException(nameof(face)),
    };

    public static string FamilyName(FontFace face) => face switch
    {
        FontFace.Regular or FontFace.Bold or FontFace.Italic => LatinFamily,
        FontFace.Devanagari => DevanagariFamily,
        FontFace.Telugu => TeluguFamily,
        FontFace.Tamil => TamilFamily,
        _ => throw new ArgumentOutOfRangeException(nameof(face)),
    };

    public static string PathOf(FontFace face) => Path.Combine(FontDirectory, FileName(face));

    /// <summary>Returns a shared copy of the font file. Callers must not modify it.</summary>
    public static byte[] Bytes(FontFace face)
    {
        lock (CacheLock)
        {
            if (!Cache.TryGetValue(face, out var bytes))
                Cache[face] = bytes = File.ReadAllBytes(PathOf(face));
            return bytes;
        }
    }

    public static string Sha256(FontFace face) => Convert.ToHexStringLower(SHA256.HashData(Bytes(face)));

    private static string FindDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("UPDF_BENCH_FONTS");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(Path.Combine(overridePath, FileName(FontFace.Regular))))
            return overridePath;
        var candidates = new List<string> { Path.Combine(AppContext.BaseDirectory, "assets", "fonts") };
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            candidates.Add(Path.Combine(dir.FullName, "assets", "fonts"));
        foreach (var candidate in candidates)
            if (File.Exists(Path.Combine(candidate, FileName(FontFace.Regular))))
                return candidate;
        throw new FileNotFoundException("The Noto font assets were not found. Set UPDF_BENCH_FONTS to the assets/fonts folder.");
    }
}
