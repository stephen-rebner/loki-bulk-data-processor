using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    public interface ITempTable
    {
        void Create(IEnumerable<string> sourceColumnNames, SqlConnection sqlConnection);
        void DropIfExists(SqlConnection sqlConnection);
    }
}
