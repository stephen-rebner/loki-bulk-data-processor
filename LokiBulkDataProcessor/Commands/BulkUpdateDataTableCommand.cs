using Loki.BulkDataProcessor.Commands.Abstract;
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
        private readonly ITempTable _tempTable;
        private readonly ISqlCommand _sqlCommand;
        private readonly DataTable _dataToCopy;

        public BulkUpdateDataTableCommand(
            IAppContext appContext, 
            ITempTable tempTable, 
            ISqlCommand sqlCommand)
        {
            _appContext = appContext;
            _tempTable = tempTable;
            _sqlCommand = sqlCommand;
        }

        public async Task Execute(DataTable dataToProcess, string destinationTableName)
        {
            var mapping = _appContext.DataTableMappingCollection.GetMappingFor(destinationTableName);
            ThrowExecptionIfMappingIsNull(mapping);

            try
            {
                //AddMappings(mapping);
                //var sourceColumns = mapping.MappingInfo.MappingMetaDataCollection.Select(metaData => metaData.SourceColumn);
                //_tempTable.Create(sourceColumns, _sqlConnection);

                //SqlBulkCopy.DestinationTableName = DbConstants.TempTableName;
                //await SqlBulkCopy.WriteToServerAsync(_dataToCopy);

                //var batches = Math.Ceiling((double)_dataToCopy.Rows.Count / _appContext.BatchSize);

                //for (var i = 1; i <= batches; i++)
                //{
                //    var updateStatement = BuildUpdateStatement(mapping);
                //    _sqlCommand.Execute(updateStatement, _sqlConnection);
                //}
            }
            catch(Exception e)
            {
                //ThrowInvalidOperationException(e.Message);
            }
            finally
            {
                //_tempTable.DropIfExists(_sqlConnection);
                //Dispose();
            }
        }

        private void ThrowExecptionIfMappingIsNull(AbstractMapping mapping)
        {
            if(mapping == null)
            {
                //ThrowInvalidOperationException("A mapping is required for bulk updates which has a primary key specified.");
            }
        }

        private string BuildUpdateStatement(AbstractMapping mapping)
        {
            var sqlBuilder = new StringBuilder();
            //sqlBuilder.Append($"UPDATE {_destinationTableName} dest");
            sqlBuilder.Append("SET ");

            foreach(var mappingValues in mapping.MappingInfo.MappingMetaDataCollection)
            {
                
            }
            
            sqlBuilder.Append($"INNER JOIN { DbConstants.TempTableName } t ON t.");

            return sqlBuilder.ToString();
        }
    }
}
