using Loki.BulkDataProcessor.Constants;
using System.Text;

namespace Loki.BulkDataProcessor.SqlBuilders
{
    internal static class TableInfo
    {
        internal static string GenerateDatabaseTableInfoQuery(string destinationTableName, string databaseName)
        {
            var tableNameElements = destinationTableName.Split('.');
            var tableElementsLength = tableNameElements.Length;

            var schemaName = tableElementsLength > 1 ? tableNameElements[tableElementsLength -2] : "dbo";
            var tableName = tableNameElements[tableElementsLength - 1];

            return @$"SELECT 
                    c.{ DestTableInfoColumns.COLUMN_NAME }, { DestTableInfoColumns.DATA_TYPE }, tc.{DestTableInfoColumns.CONSTRAINT_TYPE}
                    FROM INFORMATION_SCHEMA.COLUMNS c 
                    left join INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE cu on cu.COLUMN_NAME = c.COLUMN_NAME 
                    and cu.TABLE_NAME = '{ tableName }' and cu.TABLE_SCHEMA = '{ schemaName }'
                    left join INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc on tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME
                    WHERE c.TABLE_NAME = '{ tableName }' and c.TABLE_CATALOG = '{ databaseName }' and c.TABLE_SCHEMA = '{ schemaName }'
                    ORDER BY ORDINAL_POSITION";
        }
    }
}
