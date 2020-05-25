using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.Commands
{
    public class SqlServerCommand : ISqlCommand
    {
        private readonly IAppContext _appContext;

        public SqlServerCommand(IAppContext appContext)
        {
            _appContext = appContext;
        }

        public void Execute(string sqlCommandText, SqlConnection sqlConnection)
        {
            using var sqlCommand = new SqlCommand(sqlCommandText, sqlConnection)
            {
                CommandTimeout = _appContext.Timeout
            };

            sqlCommand.ExecuteNonQuery();
        }
    }
}
