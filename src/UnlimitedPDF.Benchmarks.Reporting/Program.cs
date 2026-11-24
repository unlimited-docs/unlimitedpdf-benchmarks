namespace UnlimitedPDF.Benchmarks.Reporting;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine("UnlimitedPDF.Benchmarks.Reporting <run-folder>");
            Console.WriteLine("  Reads manifest.json, validation.json, bdn/results/*-report-full.json and profiler/results.json and writes");
            Console.WriteLine("  report.html, summary.md, results.csv and results.json into the same folder.");
            return args.Length == 0 ? 1 : 0;
        }
        var folder = Path.GetFullPath(args[0]);
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"Run folder not found: {folder}");
            return 2;
        }
        var data = RunData.Load(folder);
        HtmlReport.Write(data, Path.Combine(folder, "report.html"));
        DataWriters.WriteMarkdown(data, Path.Combine(folder, "summary.md"));
        DataWriters.WriteCsv(data, Path.Combine(folder, "results.csv"));
        DataWriters.WriteJson(data, Path.Combine(folder, "results.json"));
        Console.WriteLine($"Wrote report.html, summary.md, results.csv and results.json to {folder} ({data.Rows.Count} cases, {data.Profiler.Count} profiler results).");
        return 0;
    }
}
