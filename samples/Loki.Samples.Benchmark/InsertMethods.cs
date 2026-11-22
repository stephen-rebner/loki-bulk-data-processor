using Loki.BulkDataProcessor;

namespace Loki.Samples.Benchmark;

/// <summary>One way of inserting the rows. Only <see cref="InsertAsync"/> is timed.</summary>
internal abstract class InsertMethod
{
    public abstract string Name(BenchmarkTarget target);

    public abstract Task InsertAsync(BenchmarkTarget target, List<BenchmarkRow> rows, CancellationToken ct);
}

/// <summary>One <c>SaveAsync</c> call in Loki's own transaction, default BatchSize (5000).</summary>
internal sealed class LokiInsert(IBulkProcessorFactory factory) : InsertMethod
{
    public override string Name(BenchmarkTarget target) => $"Loki ({target.LokiProvider})";

    public override Task InsertAsync(BenchmarkTarget target, List<BenchmarkRow> rows, CancellationToken ct)
    {
        IBulkProcessor processor = factory.Create(target.LokiProvider, target.ConnectionString);
        return processor.SaveAsync(rows, BenchmarkDbContext.TableName, ct);
    }
}

/// <summary>EF Core as most applications use it: <c>AddRange</c> + one <c>SaveChangesAsync</c> on a new context.</summary>
internal sealed class EfCoreInsert : InsertMethod
{
    public override string Name(BenchmarkTarget target) => "EF Core AddRange + SaveChanges";

    public override async Task InsertAsync(BenchmarkTarget target, List<BenchmarkRow> rows, CancellationToken ct)
    {
        await using BenchmarkDbContext db = target.CreateDbContext();
        db.BenchmarkRows.AddRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
