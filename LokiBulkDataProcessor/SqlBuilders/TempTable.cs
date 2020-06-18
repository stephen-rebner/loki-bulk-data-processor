using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Loki.BulkDataProcessor.SqlBuilders
{
    internal static class TempTable
    {
        internal static string GenerateCreateSqlStatement(IEnumerable<string> sourceColumnNames)
        {
            var queryBuilder = new StringBuilder();

            queryBuilder.AppendLine($"CREATE TABLE { DbConstants.TempTableName }");
            queryBuilder.AppendLine("(");

            foreach(var column in sourceColumnNames)
            {
                if(sourceColumnNames.Last().Equals(column, StringComparison.Ordinal))
                {
                    queryBuilder.AppendLine($"  { column }");
                }
                else
                {
                    queryBuilder.AppendLine($"  { column },");
                }
            }

            queryBuilder.AppendFormat(")");

           return queryBuilder.ToString();
        }

        internal static string GenerateDropSqlStatement()
        {
            return $"IF OBJECT_ID('tempdb..#{ DbConstants.TempTableName }') IS NOT NULL DROP TABLE { DbConstants.TempTableName }";
        }
    }
}
