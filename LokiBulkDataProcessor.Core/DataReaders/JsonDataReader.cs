using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Loki.BulkDataProcessor.Utils.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        private readonly ILogger _logger;
    
        public string TableName { get; private set; }
        
        private JsonDataReader(JsonDocument jsonDocument, ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
            _jsonDocument = jsonDocument;
        }

        /// <summary>
        /// Creates a JsonDataReader from a JsonDocument. The caller is responsible for disposing the JsonDocument.
        /// </summary>
        public static JsonDataReader Create(JsonDocument jsonDocument, ILogger logger = null)
        {
            ArgumentNullException.ThrowIfNull(jsonDocument);
            
            var reader = new JsonDataReader(jsonDocument, logger);
            reader.Initialize();
            return reader;
        }

        private void Initialize()
        {
            _logger.LogInformation("Initializing JsonDataReader");

            try
            {
                ValidateSchema();
                var root = _jsonDocument.RootElement;
                
                ExtractTableName(root);
                ExtractColumnMetadata(root);
                InitializeDataEnumerator(root);
                
                _logger.LogInformation("JsonDataReader initialized successfully for table {TableName}", MessageText.Sanitize(TableName));
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON format encountered during JsonDataReader initialization");
                throw new InvalidDataException("Invalid JSON format. Expected schema with 'tableName', 'columns' with name/type properties, and 'data'.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing JsonDataReader");
                throw;
            }
        }

        private void ValidateSchema()
        {
            JsonSchemaValidator.ValidateJsonSchema(_jsonDocument);
            _logger.LogInformation("JSON schema validated successfully");
        }

        private void ExtractTableName(JsonElement root)
        {
            TableName = root.GetProperty(JsonSchemaConstants.TableName).GetString();
            _logger.LogInformation("Reading data for table {TableName}", MessageText.Sanitize(TableName));
        }

        private void ExtractColumnMetadata(JsonElement root)
        {
            var columnsElement = root.GetProperty(JsonSchemaConstants.Columns);
            foreach (var column in columnsElement.EnumerateArray())
            {
                var columnName = column.GetProperty(JsonSchemaConstants.ColumnName).GetString();
                var columnType = column.GetProperty(JsonSchemaConstants.ColumnType).GetString();

                _columnNames.Add(columnName);
                _columnTypes[columnName!] = columnType;
            }
            
            _logger.LogDebug("Extracted {ColumnCount} columns from schema", _columnNames.Count);
        }

        private void InitializeDataEnumerator(JsonElement root)
        {
            _dataEnumerator = root.GetProperty(JsonSchemaConstants.Data).EnumerateArray();
        }
   
        public bool Read()
        {
            try
            {
                if (!_dataEnumerator.MoveNext())
                {
                    _logger.LogInformation("Reached end of data rows in JsonDataReader");
                    return false;
                }

                _currentRow = _dataEnumerator.Current;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading next row in JsonDataReader");
                throw;
            }
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
                return ConvertOrThrowDataFree(property, columnName, _columnTypes[columnName]);
            }
    
            return DBNull.Value;
        }

        /// <summary>
        /// BCL parse/convert errors quote the offending value (row data, CWE-209), so they are replaced by a message that
        /// names only the column and the declared type. The BCL exception is deliberately NOT kept as InnerException.
        /// </summary>
        private static object ConvertOrThrowDataFree(JsonElement element, string columnName, string dataType)
        {
            try
            {
                return ConvertJsonElementToObject(element, dataType);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException)
            {
                throw new FormatException(
                    $"A value in JSON column '{MessageText.Sanitize(columnName)}' could not be converted to the declared type '{MessageText.Sanitize(dataType)}'.");
            }
        }
    
        /// <summary>
        /// Converts one JSON value to the declared type. Numeric types need a JSON number that fits the type (a number sent as a
        /// string has always been rejected). Dates are parsed with the invariant culture when they are not ISO 8601, so the host's
        /// culture never changes the data (day and month do not flip). Booleans are strict: true/false only. Anything else throws,
        /// and <see cref="ConvertOrThrowDataFree"/> turns that into a data-free <see cref="FormatException"/>.
        /// </summary>
        private static object ConvertJsonElementToObject(JsonElement element, string dataType)
        {
            if (element.ValueKind == JsonValueKind.Null)
                return DBNull.Value;
    
            return dataType.ToLower() switch
            {
                DataTypeConstants.Int or DataTypeConstants.Int32 => Number(element).TryGetInt32(out int intValue) ? intValue : throw new FormatException(),
                DataTypeConstants.Long or DataTypeConstants.Int64 => Number(element).TryGetInt64(out long longValue) ? longValue : throw new FormatException(),
                DataTypeConstants.Decimal => Number(element).TryGetDecimal(out decimal decimalValue) ? decimalValue : throw new FormatException(),
                DataTypeConstants.Double => Number(element).TryGetDouble(out double doubleValue) ? doubleValue : throw new FormatException(),
                DataTypeConstants.Float or DataTypeConstants.Single => Number(element).TryGetSingle(out float floatValue) ? floatValue : throw new FormatException(),
                DataTypeConstants.Boolean or DataTypeConstants.Bool => ToBoolean(element),
                DataTypeConstants.DateTime or DataTypeConstants.Date => element.TryGetDateTime(out DateTime dateValue) ? dateValue : DateTime.Parse(StringOf(element), Invariant, DateTimeStyles.None),
                DataTypeConstants.Guid => element.TryGetGuid(out Guid guidValue) ? guidValue : Guid.Parse(StringOf(element)),
                DataTypeConstants.Byte => Number(element).TryGetByte(out byte byteValue) ? byteValue : throw new FormatException(),
                DataTypeConstants.Short or DataTypeConstants.Int16 => Number(element).TryGetInt16(out short shortValue) ? shortValue : throw new FormatException(),
                DataTypeConstants.Char => StringOf(element).Length > 0 ? StringOf(element)[0] : '\0',
                DataTypeConstants.String or _ => element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            };
        }

        private static JsonElement Number(JsonElement element) =>
            element.ValueKind == JsonValueKind.Number ? element : throw new FormatException();

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>The value as a JSON string; a number, object or array is rejected.</summary>
        private static string StringOf(JsonElement element) =>
            element.ValueKind == JsonValueKind.String ? element.GetString() : throw new FormatException();

        /// <summary>JSON true/false, or the strings "true"/"false" (any case). Anything else, including 1 and 0, is rejected
        /// rather than silently read as false.</summary>
        private static bool ToBoolean(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(element.GetString(), out bool parsed) => parsed,
            _ => throw new FormatException()
        };
    
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
            _logger.LogInformation("Closing JsonDataReader");
            _isOpen = false;
        }
    
        public void Dispose()
        {
            if (!_isOpen)
            {
                return;   // idempotent: the processor and BulkSource may both dispose it
            }

            _logger.LogInformation("Disposing JsonDataReader");
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