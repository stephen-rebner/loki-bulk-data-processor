using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using FastMember;
using Loki.BulkDataProcessor.DataReaders;
using Loki.BulkDataProcessor.Mappings;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Loki.BulkDataProcessor.Mappings.Resolution;
using Loki.BulkDataProcessor.Providers;
using Loki.BulkDataProcessor.Utils.Reflection;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.Sources;

/// <summary>
/// Turns every input shape into one <see cref="IDataReader"/> plus its <see cref="ColumnMap"/>s.
/// Disposes the readers Loki creates (ObjectReader, DataTableReader, JsonDataReader), never a caller's reader (#13).
/// </summary>
internal sealed class BulkSource : IDisposable
{
    private readonly bool _ownsReader;

    private BulkSource(IDataReader reader, IReadOnlyList<ColumnMap> columns, bool ownsReader)
    {
        Reader = reader;
        Columns = columns;
        _ownsReader = ownsReader;
    }

    public IDataReader Reader { get; }

    public IReadOnlyList<ColumnMap> Columns { get; }

    public static BulkSource FromModels<T>(IEnumerable<T> models, MappingRegistry mappings, ILogger logger) where T : class
    {
        string[] properties = typeof(T).GetPublicPropertyNames();
        LogSkippedProperties(typeof(T), logger);
        AbstractModelMapping mapping = mappings.Models.GetMappingFor(typeof(T));
        IReadOnlyList<ColumnMap> columns = ColumnMapResolver.Resolve(mapping, properties);
        ColumnMapResolver.ThrowIfSourceColumnMissing(columns, properties, "dataToProcess");
        return new BulkSource(ObjectReader.Create(models, properties), columns, ownsReader: true);
    }

    public static BulkSource FromDataTable(DataTable table, string destinationTableName, MappingRegistry mappings)
    {
        DataMapping mapping = mappings.Data.GetMappingFor(MappingKey.For(table.TableName, destinationTableName));
        string[] columns = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
        return new BulkSource(table.CreateDataReader(), ColumnMapResolver.Resolve(mapping, columns), ownsReader: true);
    }

    public static BulkSource FromDataReader(IDataReader reader, string destinationTableName, string sourceName, MappingRegistry mappings) =>
        new(reader, ResolveReaderColumns(reader, destinationTableName, sourceName, mappings), ownsReader: false);   // caller owns it

    public static BulkSource FromJson(JsonDataReader reader, MappingRegistry mappings) =>
        new(reader, ResolveReaderColumns(reader, reader.TableName, sourceName: null, mappings), ownsReader: true);

    public void Dispose()
    {
        if (_ownsReader)
        {
            Reader.Dispose();
        }
    }

    private static IReadOnlyList<ColumnMap> ResolveReaderColumns(IDataReader reader, string destinationTableName, string sourceName, MappingRegistry mappings)
    {
        DataMapping mapping = mappings.Data.GetMappingFor(MappingKey.For(sourceName, destinationTableName));
        string[] columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        return ColumnMapResolver.Resolve(mapping, columns);
    }

    private static void LogSkippedProperties(Type modelType, ILogger logger)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        string[] skipped = modelType.GetSkippedPropertyNames();
        if (skipped.Length > 0)
        {
            logger.LogDebug("Skipping properties of {ModelType} without a public getter and setter: {PropertyNames}",
                modelType.Name, string.Join(", ", skipped));
        }
    }
}
