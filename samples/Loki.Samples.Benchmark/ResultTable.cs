namespace Loki.Samples.Benchmark;

/// <summary>Prints the results as a Markdown table (median, min and max of the timed runs).</summary>
internal static class ResultTable
{
    public static void Write(TextWriter output, IReadOnlyList<MethodResult> results)
    {
        output.WriteLine();
        output.WriteLine("| Engine | Method | Rows | Median | Min | Max | Rows/s (median) | Allocated (median) | vs Loki |");
        output.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (MethodResult result in results)
        {
            output.WriteLine(RowFor(result, LokiMedianOn(results, result.Engine)));
        }
    }

    private static string RowFor(MethodResult result, TimeSpan lokiMedian)
    {
        TimeSpan median = Median(result.Times);
        double rowsPerSecond = result.Rows / Math.Max(median.TotalSeconds, 0.000_001);
        double allocatedMb = MedianOf(result.AllocatedBytes) / (1024.0 * 1024.0);
        string range = $"{Ms(median)} | {Ms(result.Times.Min())} | {Ms(result.Times.Max())}";
        return $"| {result.Engine} | {result.Method} | {result.Rows:N0} | {range} | {rowsPerSecond:N0} | {allocatedMb:N1} MB | {median / lokiMedian:N1}x |";
    }

    private static TimeSpan LokiMedianOn(IReadOnlyList<MethodResult> results, string engine) =>
        Median(results.First(r => r.Engine == engine && r.Method.StartsWith("Loki", StringComparison.Ordinal)).Times);

    private static TimeSpan Median(IReadOnlyList<TimeSpan> times) => TimeSpan.FromTicks(MedianOf(times.Select(t => t.Ticks).ToList()));

    private static long MedianOf(IReadOnlyList<long> values)
    {
        List<long> sorted = values.Order().ToList();
        int middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static string Ms(TimeSpan time) => $"{time.TotalMilliseconds:N0} ms";
}
