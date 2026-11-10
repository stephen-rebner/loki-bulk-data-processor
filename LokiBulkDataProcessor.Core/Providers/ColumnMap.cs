namespace Loki.BulkDataProcessor.Providers;

/// <summary>Maps one column of the source data to one column of the destination table.</summary>
/// <param name="SourceColumn">Column (or model property) name in the data being copied.</param>
/// <param name="DestinationColumn">Column name in the destination table.</param>
public sealed record ColumnMap(string SourceColumn, string DestinationColumn);
