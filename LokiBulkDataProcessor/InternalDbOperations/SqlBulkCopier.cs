using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace Loki.BulkDataProcessor.InternalDbOperations
{
    internal class SqlBulkCopier
    {
        private SqlBulkCopy _sqlBulkCopy;

        internal void BulkCopyDataTable(DataTable dataTable, string destinationTableName, SqlConnection sqlConnection)
        {

        }
    }
}
