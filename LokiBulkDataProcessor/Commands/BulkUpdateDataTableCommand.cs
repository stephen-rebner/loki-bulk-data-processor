using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Constants;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Mappings;
using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkUpdateDataTableCommand : IBulkDataTableCommand
    {
        private readonly IAppContext _appContext;
        private readonly ISqlDbConnection _dbConnection;


        /// todo: due to global temp tables not being able to be shared accross different connections,
        /// restructure to use base class to create temp tables and copy data to them
        public BulkUpdateDataTableCommand(IAppContext appContext, ISqlDbConnection dbConnection)
        {
            _appContext = appContext;
            _dbConnection = dbConnection;
        }

        public async Task Execute(DataTable dataToCopy, string destinationTableName)
        {
            using (_dbConnection)
            {
                _dbConnection.Open();
                using var transaction = _dbConnection.BeginTransaction();

                try
                {
                    var copyToTempTableCommand = _dbConnection.CreateNewCopyToTempTableCommand(transaction);

                    await copyToTempTableCommand.Copy(dataToCopy, destinationTableName);

                    using var query = _dbConnection.CreateQuery(transaction);

                    var test = query.Load("select * from #tempTableData");

                    var mapping = _appContext.DataTableMappingCollection.GetMappingFor(destinationTableName);

                    transaction.Commit();
                }
                catch(Exception e)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private string BuildUpdateStatement(AbstractMapping mapping, string destinationTableName)
        {
            var primaryKeyColumn = mapping.MappingInfo.MappingMetaDataCollection.FirstOrDefault(metaData => metaData.IsPrimaryKey);
            var columnsToUpdate = mapping.MappingInfo.MappingMetaDataCollection.Where(metaData => !metaData.IsPrimaryKey).Select(x => x.DestinationColumn);

            var sqlBuilder = new StringBuilder();
            sqlBuilder.AppendLine($"UPDATE dest");
            sqlBuilder.AppendLine("SET ");

            foreach(var columnName in columnsToUpdate)
            {

                sqlBuilder.Append($"dest.{columnName} = t.{columnName}");
                
                if(!columnsToUpdate.Last().Equals(columnName, StringComparison.Ordinal))
                {
                    sqlBuilder.AppendLine(", ");
                }
            }

            sqlBuilder.AppendLine($"FROM {destinationTableName} dest");

            sqlBuilder.Append($"INNER JOIN { DbConstants.TempTableName } t ON t.{primaryKeyColumn.DestinationColumn} = dest.{primaryKeyColumn.DestinationColumn}");

            return sqlBuilder.ToString();
        }
    }
}
