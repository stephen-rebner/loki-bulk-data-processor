using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.Json;

namespace Loki.BulkDataProcessor.DataReaders
{
    internal sealed class JsonDataReader : IDataReader
    {
        private readonly JsonDocument _jsonDocument;
        private readonly List<string> _columnNames = new();
        private readonly Dictionary<string, string> _columnTypes = new();
        private JsonElement.ArrayEnumerator _dataEnumerator;
        private JsonElement _currentRow;
        private bool _isOpen = true;
    
        public string TableName { get; }
        
        public JsonDataReader(Stream jsonStream)
        {
            ArgumentNullException.ThrowIfNull(jsonStream);

            try
            {
                _jsonDocument = JsonDocument.Parse(jsonStream);
                
                // Validate the JSON schema
                JsonSchemaValidator.ValidateJsonSchema(_jsonDocument);
                
                var root = _jsonDocument.RootElement;
    
                // Extract table name
                TableName = root.GetProperty(JsonSchemaConstants.TableName).GetString();
    
                // Extract column names and types
                var columnsElement = root.GetProperty(JsonSchemaConstants.Columns);
                foreach (var column in columnsElement.EnumerateArray())
                {
                    var columnName = column.GetProperty(JsonSchemaConstants.ColumnName).GetString();
                    var columnType = column.GetProperty(JsonSchemaConstants.ColumnType).GetString();
                    
                    _columnNames.Add(columnName);
                    _columnTypes[columnName] = columnType;
                }
    
                // Get enumerator for data rows
                _dataEnumerator = root.GetProperty(JsonSchemaConstants.Data).EnumerateArray();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Invalid JSON format. Expected schema with 'tableName', 'columns' with name/type properties, and 'data'.", ex);
            }
        }
    
        public bool Read()
        {
            if (!_dataEnumerator.MoveNext()) return false;
            
            _currentRow = _dataEnumerator.Current;
            
            return true;
        }
    
        public int FieldCount => _columnNames.Count;
    
        public string GetName(int i)
        {
            if (i < 0 || i >= _columnNames.Count)
                throw new IndexOutOfRangeException($"Index {i} is out of range.");
    
            return _columnNames[i];
        }
    
        public object GetValue(int i)
        {
            if (i < 0 || i >= _columnNames.Count)
                throw new IndexOutOfRangeException($"Index {i} is out of range.");
    
            var columnName = _columnNames[i];
            
            if (_currentRow.TryGetProperty(columnName, out var property))
            {
                return ConvertJsonElementToObject(property, _columnTypes[columnName]);
            }
    
            return DBNull.Value;
        }
    
        private static object ConvertJsonElementToObject(JsonElement element, string dataType)
        {
            if (element.ValueKind == JsonValueKind.Null)
                return DBNull.Value;
    
            return dataType.ToLower() switch
            {
                DataTypeConstants.Int or DataTypeConstants.Int32 => element.TryGetInt32(out int intValue) ? intValue : Convert.ToInt32(element.GetString()),
                DataTypeConstants.Long or DataTypeConstants.Int64 => element.TryGetInt64(out long longValue) ? longValue : Convert.ToInt64(element.GetString()),
                DataTypeConstants.Decimal => element.TryGetDecimal(out decimal decimalValue) ? decimalValue : Convert.ToDecimal(element.GetString()),
                DataTypeConstants.Double => element.TryGetDouble(out double doubleValue) ? doubleValue : Convert.ToDouble(element.GetString()),
                DataTypeConstants.Float or DataTypeConstants.Single => element.TryGetSingle(out float floatValue) ? floatValue : Convert.ToSingle(element.GetString()),
                DataTypeConstants.Boolean or DataTypeConstants.Bool => element.ValueKind == JsonValueKind.True || (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out bool result) && result),
                DataTypeConstants.DateTime or DataTypeConstants.Date => element.TryGetDateTime(out DateTime dateValue) ? dateValue : DateTime.Parse(element.GetString()),
                DataTypeConstants.Guid => element.TryGetGuid(out Guid guidValue) ? guidValue : Guid.Parse(element.GetString()),
                DataTypeConstants.Byte => element.TryGetByte(out byte byteValue) ? byteValue : Convert.ToByte(element.GetString()),
                DataTypeConstants.Short or DataTypeConstants.Int16 => element.TryGetInt16(out short shortValue) ? shortValue : Convert.ToInt16(element.GetString()),
                DataTypeConstants.Char => element.GetString().Length > 0 ? element.GetString()[0] : '\0',
                DataTypeConstants.String or _ => element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            };
        }
    
        public Type GetFieldType(int i)
        {
            if (i < 0 || i >= _columnNames.Count)
                throw new IndexOutOfRangeException($"Index {i} is out of range.");
    
            var columnName = _columnNames[i];
            var dataType = _columnTypes[columnName];
    
            return dataType.ToLower() switch
                {
                    DataTypeConstants.Int or DataTypeConstants.Int32 => typeof(int),
                    DataTypeConstants.Long or DataTypeConstants.Int64 => typeof(long),
                    DataTypeConstants.Decimal => typeof(decimal),
                    DataTypeConstants.Double => typeof(double),
                    DataTypeConstants.Float or DataTypeConstants.Single => typeof(float),
                    DataTypeConstants.Boolean or DataTypeConstants.Bool => typeof(bool),
                    DataTypeConstants.DateTime or DataTypeConstants.Date => typeof(DateTime),
                    DataTypeConstants.Guid => typeof(Guid),
                    DataTypeConstants.Byte => typeof(byte),
                    DataTypeConstants.Short or DataTypeConstants.Int16 => typeof(short),
                    DataTypeConstants.Char => typeof(char),
                    DataTypeConstants.String or _ => typeof(string)
                };
        }
    
        public string GetDataTypeName(int i) => _columnTypes[_columnNames[i]];
    
        // Additional IDataReader implementations
        public bool IsClosed => !_isOpen;
        public int Depth => 0;
        public bool NextResult() => false;
        public int RecordsAffected => -1;
    
        public void Close()
        {
            _isOpen = false;
            _jsonDocument?.Dispose();
        }
    
        public void Dispose()
        {
            Close();
        }
    
        public int GetOrdinal(string name) => _columnNames.IndexOf(name);
    
        public bool IsDBNull(int i) => GetValue(i) is DBNull;
    
        public object this[int i] => GetValue(i);
        public object this[string name] => GetValue(GetOrdinal(name));
    
        // Type-specific getters
        public bool GetBoolean(int i) => Convert.ToBoolean(GetValue(i));
        public byte GetByte(int i) => Convert.ToByte(GetValue(i));
        public char GetChar(int i) => Convert.ToChar(GetValue(i));
        public DateTime GetDateTime(int i) => Convert.ToDateTime(GetValue(i));
        public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i));
        public double GetDouble(int i) => Convert.ToDouble(GetValue(i));
        public float GetFloat(int i) => Convert.ToSingle(GetValue(i));
        public Guid GetGuid(int i) => (Guid)GetValue(i);
        public short GetInt16(int i) => Convert.ToInt16(GetValue(i));
        public int GetInt32(int i) => Convert.ToInt32(GetValue(i));
        public long GetInt64(int i) => Convert.ToInt64(GetValue(i));
        public string GetString(int i) => Convert.ToString(GetValue(i));
    
        // Remaining required methods with minimal implementations
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferOffset, int length) => 0;
        public long GetChars(int i, long fieldOffset, char[] buffer, int bufferOffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotImplementedException();
        public DataTable GetSchemaTable() => null;
    
        public int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, FieldCount);
            for (int i = 0; i < count; i++)
            {
                values[i] = GetValue(i);
            }
            return count;
        }
    }
}