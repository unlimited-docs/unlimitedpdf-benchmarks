# Capability and implementation status

This table describes only capabilities evidenced by the cited package
documentation in [library licenses](library-licenses.md). “Not benchmarked”
does not mean unsupported; unverified capabilities must not be inferred.
PDFsharp and MigraDoc are related, not independent competitors.

| Capability | UnlimitedPDF | PDFsharp | PdfPig | Benchmark |
|---|---|---|---|---|
| Blank pages, memory output | Native | Native | Native | 1 and 10 A4 pages |
| Plain text | Native | Native drawing | Basic native creation | Not benchmarked |
| Formatted text, custom fonts, embedding | Native | Native drawing; verify specifics | TrueType support; verify specifics | Not benchmarked |
| Unicode and complex shaping | Documented for certain scripts | Unverified | ASCII in basic builder; other cases unverified | Not benchmarked |
| Images, vector graphics | Native | Native drawing | Basic drawing; image creation unverified | Not benchmarked |
| Tables, automatic pagination, headers, footers, page numbering | Native | Official companion MigraDoc for layout | Custom implementation/unverified | Not benchmarked |
| Merge, split, extract/remove/reorder pages | Merge, split, extract and remove native; reorder unverified | Manipulation native; operation specifics unverified | Merge native; rest unverified | Not benchmarked |
| Text and image extraction | Unverified | Unverified | Native | Not benchmarked |
| Metadata, encryption/decryption | Unverified | Unverified | Read metadata and encrypted PDFs; other operations unverified | Not benchmarked |
| HTML to PDF | Native | Unverified | Unverified | Not benchmarked |
| File and stream processing | Native | Native | Native | File workloads not benchmarked |
| Windows/Linux | Documented | Documented | .NET cross-platform; verify deployment | Linux tested; Windows CI configured |
| Native dependencies and concurrency | Unverified | Unverified | Unverified | Not measured |

Image-heavy, business, manipulation, extraction, HTML, multilingual,
scalability, concurrent, cold-start and stress workloads remain unimplemented.
They must not be presented as zero or as performance results.
