using Loki.BulkDataProcessor.Utils.Validation;

namespace Loki.BulkDataProcessor.Providers;

/// <summary>
/// Provider-neutral error messages that every provider uses, so callers see the same text on every database.
/// The builders sanitise their arguments (control characters replaced, long values truncated) and never include
/// driver text, so a message built here carries no row data.
/// </summary>
public static class BulkCopyErrorMessages
{
    /// <summary>A column map names a column the destination table does not have.</summary>
    public const string ColumnMappingMismatch =
        "The given column mappings does not match up with any column in the destination table.";

    /// <summary>The caller's transaction has no connection any more.</summary>
    public const string ExternalTransactionCompleted =
        "The supplied transaction has already been committed or rolled back.";

    /// <summary>A mapping names a source column that the data being copied does not contain.</summary>
    /// <param name="column">The mapped source column name; it is sanitised before it is echoed.</param>
    public static string SourceColumnNotFound(string column) =>
        $"The mapped source column '{MessageText.Sanitize(column)}' does not exist in the data being copied.";

    /// <summary>
    /// A fixed, data-free message for a failed bulk copy, for example
    /// "Bulk copy to 'dbo.Posts' failed. See InnerException for details." Use it when the driver gives no error code.
    /// Keep the driver exception as <see cref="System.Exception.InnerException"/>.
    /// </summary>
    /// <param name="tableName">The destination table; it is sanitised before it is echoed.</param>
    public static string BulkCopyFailed(string tableName) =>
        $"Bulk copy to '{MessageText.Sanitize(tableName)}' failed. See InnerException for details.";

    /// <summary>
    /// A fixed, data-free message for a failed bulk copy with the database's error code, for example
    /// "Bulk copy to 'dbo.Posts' failed (SQL Server error 547). See InnerException for details." or
    /// "Bulk copy to 'POSTS' failed (Oracle error ORA-00001). See InnerException for details."
    /// Keep the driver exception as <see cref="System.Exception.InnerException"/>.
    /// </summary>
    /// <param name="tableName">The destination table; it is sanitised before it is echoed.</param>
    /// <param name="databaseName">The database engine, for example "SQL Server" or "Oracle"; sanitised.</param>
    /// <param name="errorCode">The driver's error number or code, for example "547" or "ORA-00001"; sanitised.</param>
    public static string BulkCopyFailed(string tableName, string databaseName, string errorCode) =>
        $"Bulk copy to '{MessageText.Sanitize(tableName)}' failed ({MessageText.Sanitize(databaseName)} error {MessageText.Sanitize(errorCode)}). See InnerException for details.";
}
