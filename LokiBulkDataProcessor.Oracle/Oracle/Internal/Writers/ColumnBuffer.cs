using System;
using System.Collections.Generic;
using System.Data;

namespace Loki.BulkDataProcessor.Oracle.Internal.Writers;

/// <summary>
/// Column-major buffer of one chunk of rows for array binding: one <c>object[]</c> per column, reused between chunks.
/// Null and <see cref="DBNull"/> values are stored as <see cref="DBNull.Value"/>; every other value goes through the
/// column's conversion. Not thread-safe; one per write.
/// </summary>
internal sealed class ColumnBuffer
{
    private readonly IReadOnlyList<OracleBoundColumn> _columns;
    private readonly object[][] _values;
    private readonly int _capacity;

    public ColumnBuffer(IReadOnlyList<OracleBoundColumn> columns, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _columns = columns;
        _capacity = capacity;
        _values = new object[columns.Count][];
        for (int i = 0; i < columns.Count; i++)
        {
            _values[i] = new object[capacity];
        }
    }

    public int Count { get; private set; }

    public int Capacity => _capacity;

    public bool IsFull => Count == _capacity;

    /// <summary>Copies the current record into the next row slot.</summary>
    public void AddRow(IDataRecord record)
    {
        if (IsFull)
        {
            throw new InvalidOperationException("The column buffer is full; flush it before adding more rows.");
        }

        for (int i = 0; i < _columns.Count; i++)
        {
            _values[i][Count] = ConvertValue(record.GetValue(_columns[i].SourceOrdinal), _columns[i]);
        }

        Count++;
    }

    /// <summary>The buffered values of one column, exactly <see cref="Count"/> long (ODP.NET binds the whole array).</summary>
    public object[] ValuesFor(int column) => Count == _capacity ? _values[column] : _values[column][..Count];

    /// <summary>Empties the buffer and releases the references it held.</summary>
    public void Clear()
    {
        foreach (object[] values in _values)
        {
            Array.Clear(values, 0, Count);
        }

        Count = 0;
    }

    private static object ConvertValue(object value, OracleBoundColumn column) =>
        value is null or DBNull ? DBNull.Value : column.Binding.Convert(value);
}
