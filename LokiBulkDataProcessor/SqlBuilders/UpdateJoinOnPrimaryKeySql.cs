using Loki.BulkDataProcessor.Constants;
using System;
using System.Data;
using System.Linq;
using System.Text;

namespace Loki.BulkDataProcessor.SqlBuilders
{
    public static class UpdateJoinOnPrimaryKeySql
    {
        public static string Generate(DataTable destinationTableInfo, string destinationTableName)
        {
            var primaryKeys = destinationTableInfo
                .AsEnumerable()
                .Where(row => row.Field<string>("CONSTRAINT_TYPE") != null && row.Field<string>("CONSTRAINT_TYPE").Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                .Select(row => row.Field<string>("COLUMN_NAME"))
                .ToArray();

            var columnsToUpdate = destinationTableInfo
                .AsEnumerable()
                .Where(row => row.Field<string>("CONSTRAINT_TYPE") == null || !row.Field<string>("CONSTRAINT_TYPE").Equals("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                .Select(row => row.Field<string>("COLUMN_NAME"))
                .ToArray(); ;

            var sqlBuilder = new StringBuilder();
            
            BuildUpdateFromSql(columnsToUpdate, destinationTableName, sqlBuilder);
            BuildJoinStatement(primaryKeys, sqlBuilder);
            BuildWhereStatement(columnsToUpdate, sqlBuilder);

            return sqlBuilder.ToString();
        }

        private static void BuildUpdateFromSql(string[] columnsToUpdate, string destinationTableName, StringBuilder sqlBuilder)
        {
            sqlBuilder.AppendLine($"UPDATE dest");
            sqlBuilder.AppendLine("SET ");

            foreach (var columnName in columnsToUpdate)
            {
                sqlBuilder.Append($"   dest.{columnName} = t.{columnName}");

                if (!columnsToUpdate.Last().Equals(columnName, StringComparison.Ordinal))
                {
                    sqlBuilder.AppendLine(", ");
                }
                else
                {
                    sqlBuilder.AppendLine();
                }
            }

            sqlBuilder.AppendLine($"FROM {destinationTableName} dest");
        }

        private static void BuildJoinStatement(string[] primaryKeys, StringBuilder sqlBuilder)
        {
            sqlBuilder.Append($"INNER JOIN { DbConstants.TempTableName } t ON ");

            foreach (var primaryKey in primaryKeys)
            {
                if (!primaryKeys.First().Equals(primaryKey, StringComparison.Ordinal))
                {
                    sqlBuilder.Append("AND ");
                }

                sqlBuilder.Append($"t.{primaryKey} = dest.{primaryKey} ");
            }
        }

        private static void BuildWhereStatement(string[] columnsToUpdate, StringBuilder sqlBuilder)
        {
            sqlBuilder.Append("WHERE ");

            foreach (var columnName in columnsToUpdate)
            {
                sqlBuilder.Append($"t.{columnName} != dest.{columnName} ");

                if (!columnsToUpdate.Last().Equals(columnName, StringComparison.Ordinal))
                {
                    sqlBuilder.Append("OR ");
                }
            }
        }
    }
}
