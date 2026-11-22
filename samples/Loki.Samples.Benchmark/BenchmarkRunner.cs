using System.Diagnostics;

namespace Loki.Samples.Benchmark;

/// <summary>The timings of one method on one engine.</summary>
internal sealed record MethodResult(string Engine, string Method, int Rows, IReadOnlyList<TimeSpan> Times, IReadOnlyList<long> AllocatedBytes);

/// <summary>
/// Runs every method on one engine, honestly: an untimed warm-up of each method first (connection pools, JIT, EF Core model),
/// the table emptied before every run, the rows generated outside the timed section, the order of the methods alternated
/// between runs, and COUNT(*) checked after every run (a run that did not insert exactly the requested rows fails).
/// </summary>
internal sealed class BenchmarkRunner(IReadOnlyList<InsertMethod> methods, TextWriter output)
{
    private const int WarmUpRows = 1_000;

    public async Task<IReadOnlyList<MethodResult>> RunAsync(BenchmarkTarget target, int rows, int runs, CancellationToken ct)
    {
        await target.PrepareAsync(ct);
        foreach (InsertMethod method in methods)
        {
            await RunOnceAsync(target, method, Math.Min(rows, WarmUpRows), ct);
        }

        Dictionary<InsertMethod, List<(TimeSpan Time, long Allocated)>> measured = methods.ToDictionary(m => m, _ => new List<(TimeSpan, long)>());
        for (int run = 1; run <= runs; run++)
        {
            foreach (InsertMethod method in run % 2 == 1 ? methods : methods.Reverse())
            {
                measured[method].Add(await RunOnceAsync(target, method, rows, ct));
                output.WriteLine($"  {target.Engine,-10} run {run}/{runs}  {method.Name(target),-32} {measured[method][^1].Time.TotalMilliseconds,10:N0} ms");
            }
        }

        return methods.Select(m => new MethodResult(target.Engine, m.Name(target), rows,
            measured[m].Select(x => x.Time).ToList(), measured[m].Select(x => x.Allocated).ToList())).ToList();
    }

    private static async Task<(TimeSpan Time, long Allocated)> RunOnceAsync(BenchmarkTarget target, InsertMethod method, int rows, CancellationToken ct)
    {
        await target.EmptyTableAsync(ct);
        List<BenchmarkRow> data = BenchmarkRow.Create(rows);
        CollectGarbage();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        Stopwatch stopwatch = Stopwatch.StartNew();
        await method.InsertAsync(target, data, ct);
        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        await ThrowIfCountIsNotAsync(target, method, rows, ct);
        return (stopwatch.Elapsed, allocated);
    }

    private static async Task ThrowIfCountIsNotAsync(BenchmarkTarget target, InsertMethod method, int expected, CancellationToken ct)
    {
        long actual = await target.CountRowsAsync(ct);
        if (actual != expected)
        {
            throw new InvalidOperationException($"{method.Name(target)} on {target.Engine} left {actual} rows instead of {expected}.");
        }
    }

    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
