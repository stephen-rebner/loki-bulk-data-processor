using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Loki.BulkDataProcessor.InternalDbOperations
{
    public class TempTable : ITempTable
    {
        private readonly ISqlCommand _sqlCommand;

        public TempTable(ISqlCommand sqlCommand)
        {
            _sqlCommand = sqlCommand;
        }

        public void Create(IEnumerable<string> sourceColumnNames, SqlConnection sqlConnection)
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

            _sqlCommand.Execute(queryBuilder.ToString(), sqlConnection);
        }

        public void DropIfExists(SqlConnection sqlConnection)
        {
            _sqlCommand.Execute($"IF OBJECT_ID('tempdb..#{ DbConstants.TempTableName }') IS NOT NULL DROP TABLE { DbConstants.TempTableName }", sqlConnection);
        }
    }
}
