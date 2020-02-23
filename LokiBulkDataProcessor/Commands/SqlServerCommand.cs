using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interface;
using System.Data.SqlClient;

namespace Loki.BulkDataProcessor.Commands
{
    public class SqlServerCommand : ISqlCommand
    {
        private readonly IDbContext _dbContext;

        public SqlServerCommand(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Executes a Non Query Command against the database
        /// </summary>
        /// <param name="sqlCommandText">The query to execute</param>
        public void Execute(string sqlCommandText)
        {
            using var sqlCommand = new SqlCommand(sqlCommandText, _dbContext.SqlConnection)
            {
                CommandTimeout = _dbContext.Timeout
            };

            sqlCommand.ExecuteNonQuery();
        }
    }
}
