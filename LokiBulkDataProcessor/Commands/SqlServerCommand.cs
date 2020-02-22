using Loki.BulkDataProcessor.Commands.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace Loki.BulkDataProcessor.Commands
{
    public class SqlServerCommand : ISqlServerCommand
    {
        public void Execute(string connectionString, string commandText, int commandTimeout)
        {
            using var sqlConnection = new SqlConnection(connectionString);
            using var sqlCommand = new SqlCommand();

            sqlConnection.Open();
            sqlCommand.CommandText = commandText;
            sqlCommand.CommandTimeout = commandTimeout;
            sqlCommand.ExecuteNonQuery();
        }
    }
}
