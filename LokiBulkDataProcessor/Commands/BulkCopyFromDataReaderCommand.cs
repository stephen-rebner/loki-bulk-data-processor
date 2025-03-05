using System.Data;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System.Data.SqlClient;
using System.Linq;

namespace Loki.BulkDataProcessor.Commands;

public class BulkCopyFromDataReaderCommand(IAppContext appContext, ILokiDbConnection dbConnection) : IBulkCopyFromDataReaderCommand
{
    public async Task Execute(IDataReader dataReader, string destinationTableName)
    {
        dbConnection.Init();
        using var transaction = dbConnection.BeginTransactionIfUsingInternalTransaction();
        
        try
        {
            using var bulkCopyCommand = dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);
            
            var mapping = appContext.DataMappingCollection.GetMappingFor(destinationTableName);
            
            var columnNames = Enumerable.Range(0, dataReader.FieldCount)
                .Select(dataReader.GetName)
                .ToArray();
            
            bulkCopyCommand.MapColumns(mapping, columnNames); // there is no mapping for DataReader
            
            await bulkCopyCommand.WriteToServerAsync(dataReader, destinationTableName);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}