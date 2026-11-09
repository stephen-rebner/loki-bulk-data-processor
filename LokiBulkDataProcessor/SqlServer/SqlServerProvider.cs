namespace Loki.BulkDataProcessor.SqlServer;

/// <summary>The SQL Server provider (SqlBulkCopy on Microsoft.Data.SqlClient).</summary>
public static class SqlServerProvider
{
    /// <summary>Default registered name: pass it to <see cref="IBulkProcessorFactory.Create"/>.</summary>
    public const string Name = "SqlServer";
}
