namespace Loki.BulkDataProcessor.SqlServer;

/// <summary>The SQL Server provider (SqlBulkCopy on Microsoft.Data.SqlClient).</summary>
/// <remarks>
/// Rows are copied with <c>SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepNulls</c>. <c>FireTriggers</c> is
/// not set, so INSERT triggers on the destination table do NOT run for bulk-copied rows; do not rely on triggers for
/// auditing or derived data on tables you load with Loki.
/// </remarks>
public static class SqlServerProvider
{
    /// <summary>Default registered name: pass it to <see cref="IBulkProcessorFactory.Create"/>.</summary>
    public const string Name = "SqlServer";
}
