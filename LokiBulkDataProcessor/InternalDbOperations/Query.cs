using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System.Data;

namespace Loki.BulkDataProcessor.InternalDbOperations
{
    internal class Query : IQuery
    {
        private readonly IDbCommand _command;

        public Query(IDbCommand command)
        {
            _command = command;
        }

        public void Dispose()
        {
            _command.Dispose();
        }

        public DataTable Load(string queryText)
        {
            _command.CommandText = queryText;

            using var reader = _command.ExecuteReader();

            var queryResult = new DataTable();
            queryResult.Load(reader);

            return queryResult;
        }
    }
}
