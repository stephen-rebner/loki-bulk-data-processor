using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Loki.BulkDataProcessor.DataReaders
{
    internal static class JsonSchemaValidator
    {
        public static void ValidateJsonSchema(JsonDocument document)
        {
            var root = document.RootElement;
            
            ValidateTableName(root);

            var columnsElement = ValidateAndGetColumnsArray(root);
        
            var columnNames = ValidateAndGetColumnNames(columnsElement);
            
            // Validate column types
            ValidateColumnTypes(columnsElement);
            
           // Validate data array
            var dataElement = ValidateAndGetDataArray(root);
        
            // Optional: Basic check on first data item without validating all properties
            var firstDataItem = dataElement.EnumerateArray().FirstOrDefault();
            
            ValidateFirstDataItemHasMatchingProperty(firstDataItem, columnNames);
        }
        
        private static void ValidateTableName(JsonElement root)
        {
            if (!root.TryGetProperty(JsonSchemaConstants.TableName, out var tableNameElement) ||
                tableNameElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrEmpty(tableNameElement.GetString()))
            {
                throw new InvalidDataException($"Missing or invalid '{JsonSchemaConstants.TableName}' property.");
            }
        }
        
        private static JsonElement ValidateAndGetColumnsArray(JsonElement root)
        {
            if (!root.TryGetProperty(JsonSchemaConstants.Columns, out var columnsElement) ||
                columnsElement.ValueKind != JsonValueKind.Array ||
                !columnsElement.EnumerateArray().Any())
            {
                throw new InvalidDataException($"Missing or empty '{JsonSchemaConstants.Columns}' array.");
            }
            return columnsElement;
        }
        
        private static HashSet<string> ValidateAndGetColumnNames(JsonElement columnsElement)
        {
            var columnNames = new HashSet<string>();
            foreach (var column in columnsElement.EnumerateArray())
            {
                if (!column.TryGetProperty(JsonSchemaConstants.ColumnName, out var nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrEmpty(nameElement.GetString()))
                {
                    throw new InvalidDataException($"Column missing valid '{JsonSchemaConstants.ColumnName}' property.");
                }
        
                if (!column.TryGetProperty(JsonSchemaConstants.ColumnType, out var typeElement) ||
                    typeElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrEmpty(typeElement.GetString()))
                {
                    throw new InvalidDataException($"Column missing valid '{JsonSchemaConstants.ColumnType}' property.");
                }
        
                string columnName = nameElement.GetString();
                // Check for duplicate column names
                if (!columnNames.Add(columnName))
                {
                    throw new InvalidDataException($"Duplicate column name '{columnName}' found in the schema.");
                }
            }
            return columnNames;
        }
        
        private static void ValidateColumnTypes(JsonElement columnsElement)
        {
            foreach (var column in columnsElement.EnumerateArray())
            {
                var typeElement = column.GetProperty(JsonSchemaConstants.ColumnType);
                string columnType = typeElement.GetString();

                if (!IsValidColumnType(columnType))
                {
                    throw new InvalidDataException($"Unsupported column type '{columnType}' for column '{column.GetProperty(JsonSchemaConstants.ColumnName).GetString()}'.");
                }
            }
        }
        
        private static JsonElement ValidateAndGetDataArray(JsonElement root)
        {
            if (!root.TryGetProperty(JsonSchemaConstants.Data, out var dataElement) ||
                dataElement.ValueKind != JsonValueKind.Array ||
                !dataElement.EnumerateArray().Any())
            {
                throw new InvalidDataException($"Missing or empty '{JsonSchemaConstants.Data}' array.");
            }
            return dataElement;
        }
        
        private static void ValidateFirstDataItemHasMatchingProperty(JsonElement firstDataItem, HashSet<string> columnNames)
        {
            if (firstDataItem.ValueKind == JsonValueKind.Object)
            {
                bool hasAnyMatchingProperty = false;
                foreach (var property in firstDataItem.EnumerateObject())
                {
                    if (columnNames.Contains(property.Name))
                    {
                        hasAnyMatchingProperty = true;
                        break;
                    }
                }

                if (!hasAnyMatchingProperty)
                {
                    throw new InvalidDataException("Data items don't match any column names defined in the schema.");
                }
            }
        }
        
        private static bool IsValidColumnType(string columnType)
        {
            var validTypes = new[]
            {
                DataTypeConstants.Int,
                DataTypeConstants.Int32,
                DataTypeConstants.Long,
                DataTypeConstants.Int64,
                DataTypeConstants.Short,
                DataTypeConstants.Int16,
                DataTypeConstants.Byte,
                
                // Floating point types
                DataTypeConstants.Decimal,
                DataTypeConstants.Double,
                DataTypeConstants.Float,
                DataTypeConstants.Single,
                
                // Other types
                DataTypeConstants.Boolean,
                DataTypeConstants.Bool,
                DataTypeConstants.DateTime,
                DataTypeConstants.Date,
                DataTypeConstants.Guid,
                DataTypeConstants.Char,
                DataTypeConstants.String,
            };
            
            return validTypes.Contains(columnType.ToLower());
        }
    }
}