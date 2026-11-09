using System;
using System.Collections.Generic;
using System.Linq;
using Loki.BulkDataProcessor.Providers;

namespace Loki.BulkDataProcessor.Mappings.Resolution;

/// <summary>Turns a mapping (or its absence) into the provider-neutral column maps.</summary>
internal static class ColumnMapResolver
{
    /// <summary>With no mapping every source column maps 1:1 by name; otherwise the mapping's columns are used, in mapping order.</summary>
    public static IReadOnlyList<ColumnMap> Resolve(AbstractMapping mapping, IReadOnlyList<string> sourceColumns) =>
        mapping is null
            ? sourceColumns.Select(column => new ColumnMap(column, column)).ToArray()
            : mapping.MappingInfo.MappingMetaDataCollection
                .Select(meta => new ColumnMap(meta.SourceColumn, meta.DestinationColumn)).ToArray();

    /// <summary>
    /// Throws when a column map names a source column that is not in <paramref name="sourceColumns"/>, for example a
    /// model mapping that maps a get-only property (those are not copied, #19).
    /// </summary>
    /// <exception cref="ArgumentException">A mapped source column is missing.</exception>
    public static void ThrowIfSourceColumnMissing(IReadOnlyList<ColumnMap> columns, IReadOnlyList<string> sourceColumns, string paramName)
    {
        var missing = columns.FirstOrDefault(column => !sourceColumns.Contains(column.SourceColumn, StringComparer.Ordinal));
        if (missing != null)
        {
            throw new ArgumentException(
                $"{BulkCopyErrorMessages.SourceColumnNotFound(missing.SourceColumn)} Only public properties with a public getter and setter are copied.",
                paramName);
        }
    }
}
