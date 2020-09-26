using Loki.BulkDataProcessor.Constants;
using System;
using System.Data;
using System.Linq;
using System.Text;

namespace Loki.BulkDataProcessor.SqlBuilders
{
    internal static class TempTable
    {
        internal static string GenerateCreateStatement(DataTable destinationTableInfo)
        {
            var queryBuilder = new StringBuilder();

            queryBuilder.AppendLine($"CREATE TABLE { DbConstants.TempTableName }");

            queryBuilder.AppendLine("(");

            var lastColumnNameValue= destinationTableInfo.Rows
                .Cast<DataRow>()
                .Last()
                .Field<string>(DestTableInfoColumns.COLUMN_NAME);

            foreach (DataRow dataRow in destinationTableInfo.Rows)
            {
                var currentColumnName = dataRow[DestTableInfoColumns.COLUMN_NAME].ToString();

                if (lastColumnNameValue.Equals(currentColumnName, StringComparison.Ordinal))
                {
                    queryBuilder.AppendLine($"  { currentColumnName } { DetermineColumnType(dataRow[DestTableInfoColumns.DATA_TYPE]) }");
                }
                else
                {
                    queryBuilder.AppendLine($"  { dataRow[DestTableInfoColumns.COLUMN_NAME] } {DetermineColumnType(dataRow[DestTableInfoColumns.DATA_TYPE])},");
                }
            }

            queryBuilder.AppendFormat(")");

           return queryBuilder.ToString();
        }

        internal static string GenerateDropStatement()
        {
            return $"IF OBJECT_ID('tempdb..#{ DbConstants.TempTableName }') IS NOT NULL DROP TABLE { DbConstants.TempTableName }";
        }

        private static string DetermineColumnType(object columnType)
        {
            var convertedColumnType = columnType.ToString();

            return convertedColumnType.Equals("NVARCHAR", StringComparison.OrdinalIgnoreCase) || convertedColumnType.Equals("VARCHAR", StringComparison.OrdinalIgnoreCase) 
                ? $"{ convertedColumnType } (max)" : convertedColumnType;
        }
    }
}
