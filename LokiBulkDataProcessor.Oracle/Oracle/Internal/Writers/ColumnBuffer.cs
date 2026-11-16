using System;
using System.Collections.Generic;
using System.Data;

namespace Loki.BulkDataProcessor.Oracle.Internal.Writers;

/// <summary>
/// Column-major buffer of one chunk of rows for array binding: one <c>object[]</c> per column, reused between chunks.
/// Null and <see cref="DBNull"/> values are stored as <see cref="DBNull.Value"/>; every other value goes through the
/// column's conversion. A chunk is also full once its string and binary values reach <see cref="MaxPayloadBytes"/>, so wide
/// rows cannot grow one chunk without bound. Not thread-safe; one per write.
/// </summary>
internal sealed class ColumnBuffer
{
    private readonly IReadOnlyList<OracleBoundColumn> _columns;
    private readonly object[][] _values;
    private readonly int _capacity;
    private readonly long _maxPayloadBytes;
    private long _payloadBytes;

    /// <summary>Default byte budget for the string and binary values of one chunk (64 MB).</summary>
    internal const long MaxPayloadBytes = 64L * 1024 * 1024;

    public ColumnBuffer(IReadOnlyList<OracleBoundColumn> columns, int capacity, long maxPayloadBytes = MaxPayloadBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPayloadBytes, 1);
        _columns = columns;
        _capacity = capacity;
        _maxPayloadBytes = maxPayloadBytes;
        _values = new object[columns.Count][];
        for (int i = 0; i < columns.Count; i++)
        {
            _values[i] = new object[capacity];
        }
    }

    public int Count { get; private set; }

    public int Capacity => _capacity;

    /// <summary>The approximate size of the string (UTF-16) and binary values buffered so far.</summary>
    public long PayloadBytes => _payloadBytes;

    public bool IsFull => Count == _capacity || _payloadBytes >= _maxPayloadBytes;

    /// <summary>Copies the current record into the next row slot.</summary>
    public void AddRow(IDataRecord record)
    {
        if (IsFull)
        {
            throw new InvalidOperationException("The column buffer is full; flush it before adding more rows.");
        }

        for (int i = 0; i < _columns.Count; i++)
        {
            object value = ConvertValue(record.GetValue(_columns[i].SourceOrdinal), _columns[i]);
            _payloadBytes += PayloadSize(value);
            _values[i][Count] = value;
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
        _payloadBytes = 0;
    }

    private static long PayloadSize(object value) => value switch
    {
        string text => text.Length * 2L,
        byte[] bytes => bytes.Length,
        _ => 0
    };

    private static object ConvertValue(object value, OracleBoundColumn column) =>
        value is null or DBNull ? DBNull.Value : column.Binding.Convert(value);
}
