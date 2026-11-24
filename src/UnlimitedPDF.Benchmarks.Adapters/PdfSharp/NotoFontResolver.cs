using PdfSharp.Fonts;
using UnlimitedPDF.Benchmarks.Core.Fixtures;

namespace UnlimitedPDF.Benchmarks.Adapters.PdfSharp;

/// <summary>
/// PDFsharp 6 needs an IFontResolver to draw any text on every platform. This resolver serves the same Noto font files every
/// other library receives. It is installed once per process, as PDFsharp requires.
/// </summary>
public sealed class NotoFontResolver : IFontResolver
{
    private static readonly Lock InstallLock = new();
    private static bool _installed;

    public static void Install()
    {
        lock (InstallLock)
        {
            if (_installed)
                return;
            if (GlobalFontSettings.FontResolver is null)
                GlobalFontSettings.FontResolver = new NotoFontResolver();
            _installed = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (familyName.Equals(FontAssets.LatinFamily, StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo(isBold ? nameof(FontFace.Bold) : isItalic ? nameof(FontFace.Italic) : nameof(FontFace.Regular));
        if (familyName.Equals(FontAssets.DevanagariFamily, StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo(nameof(FontFace.Devanagari));
        if (familyName.Equals(FontAssets.TeluguFamily, StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo(nameof(FontFace.Telugu));
        if (familyName.Equals(FontAssets.TamilFamily, StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo(nameof(FontFace.Tamil));
        return new FontResolverInfo(nameof(FontFace.Regular));
    }

    public byte[]? GetFont(string faceName) =>
        Enum.TryParse<FontFace>(faceName, out var face) ? FontAssets.Bytes(face) : null;
}
