namespace UnlimitedPDF.Benchmarks.Core;

public static class Statistics
{
    /// <summary>Percentile with linear interpolation between closest ranks (the R-7 / Excel PERCENTILE.INC method).</summary>
    public static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        ArgumentNullException.ThrowIfNull(sortedValues);
        if (sortedValues.Count == 0)
            throw new ArgumentException("At least one value is required.", nameof(sortedValues));
        ArgumentOutOfRangeException.ThrowIfLessThan(percentile, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percentile, 100);
        if (sortedValues.Count == 1)
            return sortedValues[0];
        var rank = percentile / 100 * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * (rank - lower);
    }

    public static double Mean(IReadOnlyList<double> values) => values.Count == 0 ? double.NaN : values.Average();

    public static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
            return 0;
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1));
    }

    /// <summary>Ratio of competitor to baseline, or null when either value is missing, non-finite, or the baseline is not positive.</summary>
    public static double? Ratio(double? competitor, double? baseline)
    {
        if (competitor is not { } c || baseline is not { } b)
            return null;
        if (!double.IsFinite(c) || !double.IsFinite(b) || b <= 0 || c < 0)
            return null;
        return c / b;
    }
}
