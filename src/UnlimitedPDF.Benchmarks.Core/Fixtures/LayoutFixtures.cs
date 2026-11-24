using System.Globalization;

namespace UnlimitedPDF.Benchmarks.Core.Fixtures;

public enum CellAlignment
{
    Left,
    Right,
}

/// <summary>A table definition shared by tables, business documents and reports.</summary>
public sealed record TableSpec(
    IReadOnlyList<string> Headers,
    IReadOnlyList<double> ColumnWidths,
    IReadOnlyList<CellAlignment> Alignments,
    IReadOnlyList<TableRow> Rows,
    bool RepeatHeader = true,
    string? Caption = null)
{
    public double Width => ColumnWidths.Sum();
}

/// <summary>A row of cell texts; <see cref="Image"/> optionally sits above the first cell's text.</summary>
public sealed record TableRow(IReadOnlyList<string> Cells, FixtureImage? Image = null);

public static class TableFixtures
{
    public static readonly string[] Departments = ["Engineering", "Finance", "Operations", "Sales", "Support", "Marketing", "Legal"];

    public static string RowId(int row) => $"R{row:D6}";

    /// <summary>A simple four-column employee table with a repeated header row and right-aligned amounts.</summary>
    public static TableSpec Simple(int rows)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        var random = new Random(6000 + rows);
        var data = new TableRow[rows];
        for (var i = 0; i < rows; i++)
        {
            var amount = random.Next(1_000, 999_999) / 100m;
            data[i] = new TableRow([RowId(i + 1), $"Employee {i + 1}", Departments[random.Next(Departments.Length)],
                amount.ToString("N2", CultureInfo.InvariantCulture)]);
        }
        return new TableSpec(["No.", "Name", "Department", "Amount"], [80, 175, 140, 100],
            [CellAlignment.Left, CellAlignment.Left, CellAlignment.Left, CellAlignment.Right], data);
    }

    /// <summary>A product table with a distinct 64 × 48 px PNG thumbnail in the first cell of every row.</summary>
    public static TableSpec WithImages(int rows)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        var random = new Random(6500 + rows);
        var data = new TableRow[rows];
        for (var i = 0; i < rows; i++)
        {
            var image = ImageFixtures.Create(2000 + i, ImageFormat.Png, 64, 48);
            data[i] = new TableRow([RowId(i + 1), $"Product {Words.Word(random)} {i + 1}",
                (random.Next(100, 99_999) / 100m).ToString("N2", CultureInfo.InvariantCulture)], image);
        }
        return new TableSpec(["Item", "Description", "Price"], [110, 275, 110],
            [CellAlignment.Left, CellAlignment.Left, CellAlignment.Right], data);
    }
}

/// <summary>Flowing body text with a header and a "Page i of N" footer on each page and an explicit break after each page's lines.</summary>
public sealed class HeaderFooterDocument
{
    public const int LinesPerPage = 30;
    public const string HeaderText = "Benchmark quarterly report";

    private HeaderFooterDocument(IReadOnlyList<IReadOnlyList<string>> pages) => Pages = pages;

    public IReadOnlyList<IReadOnlyList<string>> Pages { get; }

    public int PageCount => Pages.Count;

    public static string FooterText(int page, int pages) => $"Page {page} of {pages}";

    public static HeaderFooterDocument Create(int pages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pages);
        var random = new Random(7000 + pages);
        var result = new List<IReadOnlyList<string>>(pages);
        for (var p = 1; p <= pages; p++)
            result.Add(Enumerable.Range(1, LinesPerPage)
                .Select(l => $"{TextDocument.LineId(p, l)} {Words.Sentence(random, 6, 9)}").ToArray());
        return new HeaderFooterDocument(result);
    }
}
