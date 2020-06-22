using Loki.BulkDataProcessor.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Loki.BulkDataProcessor.SqlBuilders
{
    internal static class TempTableSqlGenerator
    {
        internal static string GenerateCreateStatement(IEnumerable<string> sourceColumnNames)
        {
            var queryBuilder = new StringBuilder();

            queryBuilder.AppendLine($"CREATE TABLE { DbConstants.TempTableName }");
            queryBuilder.AppendLine("(");

            foreach(var column in sourceColumnNames)
            {
                if(sourceColumnNames.Last().Equals(column, StringComparison.Ordinal))
                {
                    queryBuilder.AppendLine($"  { column } NVARCHAR (MAX)");
                }
                else
                {
                    queryBuilder.AppendLine($"  { column } NVARCHAR (MAX),");
                }
            }

            queryBuilder.AppendFormat(")");

           return queryBuilder.ToString();
        }

        internal static string GenerateDropStatement()
        {
            return $"IF OBJECT_ID('tempdb..#{ DbConstants.TempTableName }') IS NOT NULL DROP TABLE { DbConstants.TempTableName }";
        }
    }
}
