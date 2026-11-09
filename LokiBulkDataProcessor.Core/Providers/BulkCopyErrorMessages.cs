namespace Loki.BulkDataProcessor.Providers;

/// <summary>Provider-neutral error messages that every provider uses, so callers see the same text on every database.</summary>
public static class BulkCopyErrorMessages
{
    /// <summary>A column map names a column the destination table does not have.</summary>
    public const string ColumnMappingMismatch =
        "The given column mappings does not match up with any column in the destination table.";

    /// <summary>The caller's transaction has no connection any more.</summary>
    public const string ExternalTransactionCompleted =
        "The supplied transaction has already been committed or rolled back.";

    /// <summary>A mapping names a source column that the data being copied does not contain.</summary>
    public static string SourceColumnNotFound(string column) =>
        $"The mapped source column '{column}' does not exist in the data being copied.";
}
