using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using UnlimitedPDF.Benchmarks.Adapters.PdfSharp;
using UnlimitedPDF.Benchmarks.Core;
using UnlimitedPDF.Benchmarks.Core.Fixtures;
using MdDocument = MigraDoc.DocumentObjectModel.Document;

namespace UnlimitedPDF.Benchmarks.Adapters.MigraDoc;

/// <summary>
/// PDFsharp-MigraDoc 6.2.4 (MIT), PDFsharp's official document-layout companion: tables with repeated heading rows, headers,
/// footers with page fields and flowing paragraphs, rendered by PdfDocumentRenderer. In-memory images can only be passed to
/// MigraDoc as "base64:" strings, so the encoding is part of the measured operation. One instance per thread.
/// </summary>
public sealed class MigraDocAdapter : ITableGenerator, IHeaderFooterGenerator, IBusinessDocumentGenerator
{
    /// <summary>Matches UnlimitedPDF's fixed table-cell text size so both tables carry the same text at the same size.</summary>
    public const double TableFontSize = 10;

    public MigraDocAdapter() => NotoFontResolver.Install();

    public string LibraryId => LibraryIds.MigraDoc;

    public byte[] CreateTableDocument(TableSpec table)
    {
        var document = NewDocument(out var section);
        AddTable(section, table);
        return Render(document);
    }

    public byte[] CreateHeaderFooterDocument(HeaderFooterDocument document)
    {
        var md = NewDocument(out var section);
        section.PageSetup.TopMargin = Unit.FromPoint(70);
        section.PageSetup.BottomMargin = Unit.FromPoint(60);
        section.PageSetup.HeaderDistance = Unit.FromPoint(48);
        section.PageSetup.FooterDistance = Unit.FromPoint(40);
        var header = section.Headers.Primary.AddParagraph(HeaderFooterDocument.HeaderText);
        header.Format.Font.Bold = true;
        header.Format.Font.Size = Unit.FromPoint(12);
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Right;
        footer.Format.Font.Size = Unit.FromPoint(9);
        footer.AddText("Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();
        for (var p = 0; p < document.PageCount; p++)
        {
            if (p > 0)
                section.AddPageBreak();
            foreach (var line in document.Pages[p])
            {
                var paragraph = section.AddParagraph(line);
                paragraph.Format.Font.Size = Unit.FromPoint(11);
                paragraph.Format.SpaceBefore = Unit.FromPoint(2);
            }
        }
        return Render(md);
    }

    public byte[] CreateBusinessDocument(BusinessDocument document)
    {
        var md = NewDocument(out var section);
        var title = section.AddParagraph(document.Title);
        title.Format.Font.Size = Unit.FromPoint(18);
        title.Format.Font.Bold = true;
        foreach (var (label, value) in document.Fields)
            Small(section.AddParagraph($"{label}: {value}"));
        foreach (var line in document.PartyLines)
            Small(section.AddParagraph(line));
        foreach (var table in document.Tables)
        {
            if (table.Caption is not null)
            {
                var caption = section.AddParagraph(table.Caption);
                caption.Format.Font.Size = Unit.FromPoint(12);
                caption.Format.Font.Bold = true;
                caption.Format.SpaceBefore = Unit.FromPoint(12);
                caption.Format.KeepWithNext = true;
            }
            var spacer = section.AddParagraph();
            spacer.Format.SpaceBefore = Unit.FromPoint(6);
            spacer.Format.Font.Size = Unit.FromPoint(1);
            AddTable(section, table);
        }
        foreach (var note in document.Notes)
        {
            var paragraph = section.AddParagraph(note);
            paragraph.Format.Font.Size = Unit.FromPoint(9);
            paragraph.Format.Font.Italic = true;
            paragraph.Format.SpaceBefore = Unit.FromPoint(6);
        }
        return Render(md);

        static void Small(Paragraph p)
        {
            p.Format.Font.Size = Unit.FromPoint(10);
            p.Format.SpaceBefore = Unit.FromPoint(3);
        }
    }

    private static MdDocument NewDocument(out Section section)
    {
        var document = new MdDocument();
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontAssets.LatinFamily;
        normal.Font.Size = Unit.FromPoint(12);
        section = document.AddSection();
        section.PageSetup = document.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.PageWidth = Unit.FromPoint(PageGeometry.Width);
        section.PageSetup.PageHeight = Unit.FromPoint(PageGeometry.Height);
        section.PageSetup.TopMargin = Unit.FromPoint(PageGeometry.Margin);
        section.PageSetup.BottomMargin = Unit.FromPoint(PageGeometry.Margin);
        section.PageSetup.LeftMargin = Unit.FromPoint(PageGeometry.Margin);
        section.PageSetup.RightMargin = Unit.FromPoint(PageGeometry.Margin);
        return document;
    }

    private static void AddTable(Section section, TableSpec spec)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Format.Font.Size = Unit.FromPoint(TableFontSize);
        for (var c = 0; c < spec.ColumnWidths.Count; c++)
        {
            var column = table.AddColumn(Unit.FromPoint(spec.ColumnWidths[c]));
            if (spec.Alignments[c] == CellAlignment.Right)
                column.Format.Alignment = ParagraphAlignment.Right;
        }
        var header = table.AddRow();
        header.HeadingFormat = spec.RepeatHeader;
        header.Shading.Color = Colors.LightGray;
        header.Format.Font.Bold = true;
        for (var c = 0; c < spec.Headers.Count; c++)
            header.Cells[c].AddParagraph(spec.Headers[c]);
        foreach (var data in spec.Rows)
        {
            var row = table.AddRow();
            for (var c = 0; c < data.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                if (c == 0 && data.Image is not null)
                {
                    var image = cell.AddImage("base64:" + Convert.ToBase64String(data.Image.Encoded));
                    image.Width = Unit.FromPoint(data.Image.PixelWidth * 0.75);
                }
                cell.AddParagraph(data.Cells[c]);
            }
        }
    }

    private static byte[] Render(MdDocument document)
    {
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var pdf = renderer.PdfDocument;
        using var stream = new MemoryStream();
        pdf.Save(stream, false);
        return stream.ToArray();
    }
}
