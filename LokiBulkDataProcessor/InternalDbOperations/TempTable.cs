using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System.Collections.Generic;
using System.Data.SqlClient;

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
            throw new System.NotImplementedException();
        }

        public void DropIfExists(SqlConnection sqlConnection)
        {
            _sqlCommand.Execute($"IF OBJECT_ID('tempdb..#{DbConstants.TempTableName}') IS NOT NULL DROP TABLE { DbConstants.TempTableName }", sqlConnection);
        }
    }
}
