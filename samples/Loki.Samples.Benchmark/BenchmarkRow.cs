using Loki.BulkDataProcessor.Mappings;

namespace Loki.Samples.Benchmark;

/// <summary>One row of the BenchmarkRows table; both EF Core and Loki insert these.</summary>
internal sealed class BenchmarkRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public int Quantity { get; set; }

    public DateTime CreatedOn { get; set; }

    /// <summary>New rows with the same values for every method; generated outside the timed section.</summary>
    public static List<BenchmarkRow> Create(int count)
    {
        DateTime createdOn = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        return Enumerable.Range(1, count)
            .Select(i => new BenchmarkRow { Name = $"Row {i}", Amount = 1m + i % 1000 * 0.25m, Quantity = i % 50, CreatedOn = createdOn.AddSeconds(i) })
            .ToList();
    }
}

/// <summary>Loki copies the four data columns. Id is the identity column, which the database fills in (as it does for EF Core).</summary>
internal sealed class BenchmarkRowMapping : ModelMapping<BenchmarkRow>
{
    public BenchmarkRowMapping()
    {
        Map(row => row.Name).ToDestinationColumn("Name");
        Map(row => row.Amount).ToDestinationColumn("Amount");
        Map(row => row.Quantity).ToDestinationColumn("Quantity");
        Map(row => row.CreatedOn).ToDestinationColumn("CreatedOn");
    }
}
