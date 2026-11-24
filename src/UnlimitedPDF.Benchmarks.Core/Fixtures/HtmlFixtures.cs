using System.Globalization;
using System.Net;
using System.Text;

namespace UnlimitedPDF.Benchmarks.Core.Fixtures;

public enum HtmlSize
{
    Small,
    Medium,
    Large,
}

public enum HtmlKind
{
    /// <summary>Headings and paragraphs only.</summary>
    Simple,

    /// <summary>Common CSS (classes, borders, backgrounds, colours), a table with a repeated header and embedded PNG images.</summary>
    TablesCssImages,
}

/// <summary>Self-contained HTML (images as data: URIs, no network). Advanced layout CSS such as flexbox or grid is deliberately excluded.</summary>
public sealed record HtmlDocument(HtmlKind Kind, HtmlSize Size, string Html, IReadOnlyList<string> RequiredText, int ImageCount)
{
    public const string FontFamily = FontAssets.LatinFamily;

    public static int Paragraphs(HtmlSize size) => size switch { HtmlSize.Small => 5, HtmlSize.Medium => 100, _ => 1000 };

    public static int TableRows(HtmlSize size) => size switch { HtmlSize.Small => 20, HtmlSize.Medium => 200, _ => 2000 };

    public static int Images(HtmlSize size) => size switch { HtmlSize.Small => 2, HtmlSize.Medium => 10, _ => 50 };

    public static HtmlDocument Create(HtmlKind kind, HtmlSize size) => kind == HtmlKind.Simple ? Simple(size) : Rich(size);

    private static string Head(string css) =>
        $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Benchmark</title><style>body{{font-family:'{FontFamily}',sans-serif;font-size:11pt;color:#222}}{css}</style></head><body>";

    private static HtmlDocument Simple(HtmlSize size)
    {
        var random = new Random(9000 + (int)size);
        var required = new List<string>();
        var html = new StringBuilder(Head("h1{font-size:20pt}h2{font-size:14pt}p{margin:0 0 6pt 0}"));
        html.Append("<h1>Simple HTML benchmark</h1>");
        required.Add("Simple HTML benchmark");
        var count = Paragraphs(size);
        for (var i = 1; i <= count; i++)
        {
            if (i % 10 == 1)
            {
                var heading = $"Heading {i:D4}";
                html.Append("<h2>").Append(heading).Append("</h2>");
                required.Add(heading);
            }
            var id = $"Para{i:D4}";
            html.Append("<p><b>").Append(id).Append("</b> ").Append(WebUtility.HtmlEncode(Words.Sentence(random, 25, 40))).Append("</p>");
            required.Add(id);
        }
        html.Append("</body></html>");
        return new HtmlDocument(HtmlKind.Simple, size, html.ToString(), required, 0);
    }

    private static HtmlDocument Rich(HtmlSize size)
    {
        var random = new Random(9100 + (int)size);
        var required = new List<string>();
        const string css = ".banner{background:#1f3a93;color:#fff;padding:8pt}.note{border:1px solid #b01c2e;padding:6pt;background:#fdecee}" +
                           "table{border-collapse:collapse;width:100%}th{background:#e8e8e8;text-align:left}th,td{border:1px solid #999;padding:3pt}" +
                           "td.num{text-align:right}.muted{color:#666}img{width:120pt;height:90pt;margin:4pt}";
        var html = new StringBuilder(Head(css));
        html.Append("<div class=\"banner\"><h1>Tables, CSS and images</h1></div>");
        required.Add("Tables, CSS and images");
        html.Append("<p class=\"note\">Synthetic report generated for benchmarking. <span class=\"muted\">No real data.</span></p>");
        required.Add("Synthetic report generated for benchmarking.");
        var images = Images(size);
        html.Append("<div>");
        for (var i = 0; i < images; i++)
        {
            var image = ImageFixtures.Create(4000 + i, ImageFormat.Png, 160, 120);
            html.Append("<img alt=\"Figure ").Append(i + 1).Append("\" src=\"data:image/png;base64,")
                .Append(Convert.ToBase64String(image.Encoded)).Append("\">");
        }
        html.Append("</div>");
        html.Append("<table><thead><tr><th>Id</th><th>Customer</th><th>Region</th><th>Amount</th></tr></thead><tbody>");
        required.AddRange(["Id", "Customer", "Region", "Amount"]);
        var rows = TableRows(size);
        for (var r = 1; r <= rows; r++)
        {
            var id = TableFixtures.RowId(r);
            var amount = (random.Next(1_000, 999_999) / 100m).ToString("N2", CultureInfo.InvariantCulture);
            html.Append("<tr><td>").Append(id).Append("</td><td>Customer ").Append(r).Append("</td><td>")
                .Append(TableFixtures.Departments[random.Next(TableFixtures.Departments.Length)])
                .Append("</td><td class=\"num\">").Append(amount).Append("</td></tr>");
            required.Add(id);
        }
        html.Append("</tbody></table></body></html>");
        return new HtmlDocument(HtmlKind.TablesCssImages, size, html.ToString(), required, images);
    }
}
