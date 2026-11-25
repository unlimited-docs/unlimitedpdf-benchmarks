using System.Globalization;

namespace UnlimitedPDF.Benchmarks.Core.Fixtures;

public enum BusinessDocumentKind
{
    Invoice,
    PurchaseOrder,
    Quotation,
    SalesReport,
    BankStatement,
    Payslip,
    ProductCatalog,
    FinancialReport,
    TabularReport,
}

/// <summary>
/// A library-neutral business document: a title, label/value lines, party blocks, tables and notes, laid out top to bottom
/// with automatic pagination. Every library renders exactly these elements in this order.
/// </summary>
public sealed record BusinessDocument(
    BusinessDocumentKind Kind,
    string Title,
    IReadOnlyList<(string Label, string Value)> Fields,
    IReadOnlyList<string> PartyLines,
    IReadOnlyList<TableSpec> Tables,
    IReadOnlyList<string> Notes)
{
    /// <summary>Every token the output must contain, for independent text validation.</summary>
    public IEnumerable<string> RequiredText()
    {
        yield return Title;
        foreach (var (label, value) in Fields)
        {
            yield return label;
            yield return value;
        }
        foreach (var line in PartyLines)
            yield return line;
        foreach (var table in Tables)
        {
            if (table.Caption is not null)
                yield return table.Caption;
            foreach (var header in table.Headers)
                yield return header;
            foreach (var row in table.Rows)
                foreach (var cell in row.Cells)
                    yield return cell;
        }
        foreach (var note in Notes)
            yield return note;
    }

    public int ImageCount => Tables.Sum(t => t.Rows.Count(r => r.Image is not null));
}

public static class BusinessDocuments
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static BusinessDocument Create(BusinessDocumentKind kind, int seed = 0) => kind switch
    {
        BusinessDocumentKind.Invoice => Trade(kind, "Tax invoice", "Invoice no.", 8, seed),
        BusinessDocumentKind.PurchaseOrder => Trade(kind, "Purchase order", "PO no.", 12, seed),
        BusinessDocumentKind.Quotation => Trade(kind, "Quotation", "Quote no.", 6, seed),
        BusinessDocumentKind.SalesReport => SalesReport(seed),
        BusinessDocumentKind.BankStatement => BankStatement(seed),
        BusinessDocumentKind.Payslip => Payslip(seed),
        BusinessDocumentKind.ProductCatalog => Catalog(seed),
        BusinessDocumentKind.FinancialReport => Financial(seed),
        BusinessDocumentKind.TabularReport => Tabular(seed),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string Money(decimal value) => value.ToString("N2", Invariant);

    private static IReadOnlyList<string> Parties(int seed) =>
    [
        "Seller: Example Components Ltd, 12 Market Road, Springfield",
        "Buyer: Sample Retail Co, 48 Harbour Street, Riverton",
        $"Customer reference: CUST-{seed:D5}",
    ];

    private static BusinessDocument Trade(BusinessDocumentKind kind, string title, string numberLabel, int items, int seed)
    {
        var random = new Random(8000 + (int)kind * 100 + seed);
        var rows = new List<TableRow>();
        decimal subtotal = 0;
        for (var i = 1; i <= items; i++)
        {
            var qty = random.Next(1, 20);
            var rate = random.Next(500, 50_000) / 100m;
            var amount = qty * rate;
            subtotal += amount;
            rows.Add(new TableRow([i.ToString(Invariant), $"{Words.Word(random)} {Words.Word(random)} kit {i}",
                qty.ToString(Invariant), Money(rate), Money(amount)]));
        }
        var tax = Math.Round(subtotal * 0.18m, 2);
        var items1 = new TableSpec(["No.", "Description", "Qty", "Rate", "Amount"], [35, 220, 50, 90, 100],
            [CellAlignment.Left, CellAlignment.Left, CellAlignment.Right, CellAlignment.Right, CellAlignment.Right], rows);
        var totals = new TableSpec(["Summary", "Amount"], [150, 100], [CellAlignment.Left, CellAlignment.Right],
        [
            new TableRow(["Subtotal", Money(subtotal)]),
            new TableRow(["Tax 18%", Money(tax)]),
            new TableRow(["Total due", Money(subtotal + tax)]),
        ], RepeatHeader: false);
        return new BusinessDocument(kind, title,
            [(numberLabel, $"{kind.ToString().ToUpperInvariant()[..3]}-2026-{seed + 1:D5}"), ("Date", "2026-11-24"), ("Due", "2026-12-24")],
            Parties(seed), [items1, totals],
            ["Payment is due within 30 days.", "Thank you for your business."]);
    }

    private static BusinessDocument SalesReport(int seed)
    {
        var random = new Random(8100 + seed);
        string[] regions = ["North", "South", "East", "West", "Central"];
        var rows = new List<TableRow>();
        for (var i = 1; i <= 50; i++)
        {
            var units = random.Next(100, 5_000);
            var revenue = units * random.Next(800, 9_000) / 100m;
            rows.Add(new TableRow([$"S{i:D3}", regions[i % regions.Length], $"Product line {i}", units.ToString("N0", Invariant), Money(revenue)]));
        }
        var summary = regions.Select(r => new TableRow([r, Money(random.Next(100_000, 900_000))])).ToList();
        return new BusinessDocument(BusinessDocumentKind.SalesReport, "Quarterly sales report",
            [("Period", "2026 Q3"), ("Prepared", "2026-11-24")], [],
        [
            new(["Code", "Region", "Line", "Units", "Revenue"], [50, 80, 175, 80, 110],
                [CellAlignment.Left, CellAlignment.Left, CellAlignment.Left, CellAlignment.Right, CellAlignment.Right], rows,
                Caption: "Sales by product line"),
            new(["Region", "Revenue"], [150, 120], [CellAlignment.Left, CellAlignment.Right], summary, RepeatHeader: false,
                Caption: "Regional summary"),
        ], ["Figures are synthetic and generated for benchmarking."]);
    }

    private static BusinessDocument BankStatement(int seed)
    {
        var random = new Random(8200 + seed);
        var balance = 25_000m;
        var rows = new List<TableRow>();
        var date = new DateOnly(2026, 1, 1);
        for (var i = 1; i <= 300; i++)
        {
            var credit = random.Next(3) == 0;
            var amount = random.Next(100, 250_000) / 100m;
            balance += credit ? amount : -amount;
            rows.Add(new TableRow([date.AddDays(i / 3).ToString("yyyy-MM-dd", Invariant), $"TXN{i:D5} {Words.Word(random)} {Words.Word(random)}",
                credit ? "" : Money(amount), credit ? Money(amount) : "", Money(balance)]));
        }
        return new BusinessDocument(BusinessDocumentKind.BankStatement, "Account statement",
            [("Account", "00-1234-5678"), ("Period", "2026-01-01 to 2026-04-10"), ("Opening balance", Money(25_000m))],
            ["Account holder: Sample Retail Co", "Branch: Riverton Central"],
            [new(["Date", "Description", "Debit", "Credit", "Balance"], [70, 185, 80, 80, 80],
                [CellAlignment.Left, CellAlignment.Left, CellAlignment.Right, CellAlignment.Right, CellAlignment.Right], rows)],
            [$"Closing balance: {Money(balance)}"]);
    }

    private static BusinessDocument Payslip(int seed)
    {
        var random = new Random(8300 + seed);
        string[] earnings = ["Basic salary", "House allowance", "Transport allowance", "Overtime", "Bonus"];
        string[] deductions = ["Income tax", "Pension", "Health insurance", "Professional tax"];
        var e = earnings.Select(x => new TableRow([x, Money(random.Next(10_000, 900_000) / 100m)])).ToList();
        var d = deductions.Select(x => new TableRow([x, Money(random.Next(1_000, 200_000) / 100m)])).ToList();
        return new BusinessDocument(BusinessDocumentKind.Payslip, "Payslip",
            [("Employee", $"EMP-{seed + 1:D5}"), ("Pay period", "November 2026"), ("Department", "Engineering")],
            ["Employer: Example Components Ltd"],
        [
            new(["Earnings", "Amount"], [250, 120], [CellAlignment.Left, CellAlignment.Right], e, RepeatHeader: false, Caption: "Earnings"),
            new(["Deductions", "Amount"], [250, 120], [CellAlignment.Left, CellAlignment.Right], d, RepeatHeader: false, Caption: "Deductions"),
        ], ["This payslip is computer generated."]);
    }

    private static BusinessDocument Catalog(int seed)
    {
        var random = new Random(8400 + seed);
        var rows = Enumerable.Range(1, 40).Select(i => new TableRow(
            [$"SKU-{i:D4}", $"{Words.Word(random)} {Words.Word(random)} model {i}", Money(random.Next(500, 200_000) / 100m)],
            ImageFixtures.Create(3000 + i, ImageFormat.Jpeg, 96, 72))).ToList();
        return new BusinessDocument(BusinessDocumentKind.ProductCatalog, "Product catalogue",
            [("Edition", "Winter 2026")], [],
            [new(["Product", "Description", "Price"], [120, 265, 110],
                [CellAlignment.Left, CellAlignment.Left, CellAlignment.Right], rows)],
            ["Prices exclude tax."]);
    }

    private static BusinessDocument Financial(int seed)
    {
        var random = new Random(8500 + seed);
        TableSpec Statement(string caption, string[] lines) => new(["Line item", "2026", "2025"], [255, 120, 120],
            [CellAlignment.Left, CellAlignment.Right, CellAlignment.Right],
            lines.Select(l => new TableRow([l, Money(random.Next(10_000, 99_999_999) / 100m), Money(random.Next(10_000, 99_999_999) / 100m)])).ToList(),
            RepeatHeader: false, Caption: caption);
        return new BusinessDocument(BusinessDocumentKind.FinancialReport, "Annual financial report",
            [("Company", "Example Components Ltd"), ("Year ended", "2026-03-31")], [],
        [
            Statement("Income statement", ["Revenue", "Cost of sales", "Gross profit", "Operating expenses", "Operating profit", "Finance costs", "Profit before tax", "Tax", "Profit for the year"]),
            Statement("Balance sheet", ["Property and equipment", "Intangible assets", "Inventories", "Trade receivables", "Cash", "Total assets", "Trade payables", "Borrowings", "Equity"]),
            Statement("Cash flow", ["Operating activities", "Investing activities", "Financing activities", "Net change in cash", "Opening cash", "Closing cash"]),
        ], ["Prepared under synthetic accounting policies for benchmarking only."]);
    }

    private static BusinessDocument Tabular(int seed)
    {
        var table = TableFixtures.Simple(2000) with { Caption = "Employee directory" };
        return new BusinessDocument(BusinessDocumentKind.TabularReport, "Multi-page tabular report",
            [("Rows", "2,000"), ("Generated", "2026-11-24"), ("Seed", seed.ToString(Invariant))], [], [table], ["End of report."]);
    }
}
