using Loki.BulkDataProcessor.Constants;
using System;
using System.Data;
using System.Linq;
using System.Text;

namespace Loki.BulkDataProcessor.SqlBuilders
{
    internal static class TempTableSqlGenerator
    {
        internal static string GenerateCreateStatement(DataTable destinationTableInfo)
        {
            var queryBuilder = new StringBuilder();

            queryBuilder.AppendLine($"CREATE TABLE { DbConstants.TempTableName }");

            queryBuilder.AppendLine("(");

            var lastColumnNameValue= destinationTableInfo.Rows
                .Cast<DataRow>()
                .Last()
                .Field<string>("COLUMN_NAME");

            foreach (DataRow dataRow in destinationTableInfo.Rows)
            {
                var currentColumnName = dataRow["COLUMN_NAME"].ToString();

                if (lastColumnNameValue.Equals(currentColumnName, StringComparison.Ordinal))
                {
                    queryBuilder.AppendLine($"  { currentColumnName } { DetermineColumnType(dataRow["DATA_TYPE"]) }");
                }
                else
                {
                    queryBuilder.AppendLine($"  { dataRow["COLUMN_NAME"] } {DetermineColumnType(dataRow["DATA_TYPE"])},");
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
