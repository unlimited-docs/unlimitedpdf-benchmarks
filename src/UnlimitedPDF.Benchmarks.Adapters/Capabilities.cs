using UnlimitedPDF.Benchmarks.Core.Fixtures;

namespace UnlimitedPDF.Benchmarks.Adapters;

/// <summary>
/// Base identity of every adapter. Capabilities are expressed by the narrow interfaces below: an adapter implements only what
/// the library's documented public API supports, so unsupported scenarios are detected rather than emulated.
/// Adapters prepare reusable library objects (fonts, typefaces) in their constructor, outside measured operations, and must
/// not be shared between threads unless stated.
/// </summary>
public interface IPdfLibraryAdapter
{
    string LibraryId { get; }
}

/// <summary>Blank A4 pages with no content.</summary>
public interface IEmptyDocumentGenerator : IPdfLibraryAdapter
{
    byte[] CreateEmptyDocument(int pages);
}

/// <summary>Positioned plain text drawn with the embedded Noto Sans font.</summary>
public interface ITextDocumentGenerator : IPdfLibraryAdapter
{
    byte[] CreateTextDocument(TextDocument document);

    /// <summary>The same document written to a file path through the library's file API.</summary>
    void SaveTextDocument(TextDocument document, string path);
}

/// <summary>Positioned plain text drawn with the library's built-in, non-embedded standard Helvetica (default configuration).</summary>
public interface IStandardFontTextGenerator : IPdfLibraryAdapter
{
    byte[] CreateStandardFontTextDocument(TextDocument document);
}

/// <summary>Several sizes and bold/italic faces; coloured text when <see cref="RichTextDocument.Colored"/>.</summary>
public interface IRichTextGenerator : IPdfLibraryAdapter
{
    bool SupportsTextColor { get; }

    byte[] CreateRichTextDocument(RichTextDocument document);
}

public interface IImageDocumentGenerator : IPdfLibraryAdapter
{
    byte[] CreateImageDocument(ImageDocument document);
}

/// <summary>Lines, stroked and filled rectangles and circles.</summary>
public interface IVectorGraphicsGenerator : IPdfLibraryAdapter
{
    byte[] CreateVectorDocument(VectorDocument document);
}

/// <summary>Text, an image and filled rectangles per page.</summary>
public interface IMixedContentGenerator : IPdfLibraryAdapter
{
    byte[] CreateMixedDocument(MixedDocument document);
}

/// <summary>High-level tables that paginate automatically and repeat their header row.</summary>
public interface ITableGenerator : IPdfLibraryAdapter
{
    byte[] CreateTableDocument(TableSpec table);
}

/// <summary>Repeating page header and a "Page i of N" footer around flowing body text.</summary>
public interface IHeaderFooterGenerator : IPdfLibraryAdapter
{
    byte[] CreateHeaderFooterDocument(HeaderFooterDocument document);
}

/// <summary>Flowing business documents (title, fields, parties, tables, notes) with automatic pagination.</summary>
public interface IBusinessDocumentGenerator : IPdfLibraryAdapter
{
    byte[] CreateBusinessDocument(BusinessDocument document);
}

/// <summary>Text in scripts that need complex shaping. Only scripts the library documents as shaped are reported as supported.</summary>
public interface IMultilingualGenerator : IPdfLibraryAdapter
{
    bool Supports(Language language);

    byte[] CreateMultilingualDocument(MultilingualDocument document);
}

public interface IPdfMerger : IPdfLibraryAdapter
{
    byte[] Merge(IReadOnlyList<byte[]> inputs);
}

public interface IPdfSplitter : IPdfLibraryAdapter
{
    /// <summary>One single-page PDF per input page, in page order.</summary>
    IReadOnlyList<byte[]> SplitIntoPages(byte[] input);

    /// <summary>One PDF per inclusive, ascending page range, in the order given.</summary>
    IReadOnlyList<byte[]> SplitIntoRanges(byte[] input, IReadOnlyList<(int First, int Last)> ranges);
}

public interface IPageExtractor : IPdfLibraryAdapter
{
    /// <summary>A new PDF with the selected pages in the selection's order (which also covers reordering).</summary>
    byte[] ExtractPages(byte[] input, PageSelection selection);
}

public interface IPageRemover : IPdfLibraryAdapter
{
    /// <summary>A new PDF without the selected pages; the remaining pages keep their order.</summary>
    byte[] RemovePages(byte[] input, PageSelection selection);
}

/// <summary>HTML to PDF. Implementations may hold external processes and must be disposed.</summary>
public interface IHtmlToPdfConverter : IPdfLibraryAdapter, IDisposable
{
    byte[] ConvertHtml(HtmlDocument html);
}
