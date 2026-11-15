namespace Loki.BulkDataProcessor.Mappings.Resolution;

/// <summary>
/// The one rule for finding a <see cref="DataMapping"/> (#16): a mapping applies when its SourceTableName equals the
/// input's source name. The source name is the explicit <c>sourceName</c> (IDataReader overload), otherwise a non-blank
/// <c>DataTable.TableName</c>, otherwise the destination table name (also the rule for JSON and plain readers).
/// </summary>
internal static class MappingKey
{
    public static string For(string sourceName, string destinationTableName) =>
        string.IsNullOrWhiteSpace(sourceName) ? destinationTableName : sourceName;
}
