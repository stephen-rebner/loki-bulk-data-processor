using System.Data;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using System.Data.SqlClient;
using System.Linq;
using Loki.BulkDataProcessor.InternalDbOperations.Extensions;

namespace Loki.BulkDataProcessor.Commands;

public class BulkCopyFromDataReaderCommand(IAppContext appContext, ILokiDbConnection dbConnection) : IBulkCopyFromDataReaderCommand
{
    public async Task Execute(IDataReader dataReader, string destinationTableName)
    {
        dbConnection.Init();
        var transaction = dbConnection.BeginTransactionIfUsingInternalTransaction();
        
        try
        {
            var mapping = appContext.DataMappingCollection.GetMappingFor(destinationTableName);
            
            var columnNames = Enumerable.Range(0, dataReader.FieldCount)
                .Select(dataReader.GetName)
                .ToArray();
            
            using var bulkCopyCommand = dbConnection.CreateNewBulkCopyCommand((SqlTransaction)transaction);
            
            bulkCopyCommand.MapColumns(mapping, columnNames);
            
            await bulkCopyCommand.WriteToServerAsync(dataReader, destinationTableName);
            
            transaction.CommitIfUsingInternalTransaction(appContext.IsUsingExternalTransaction);
            transaction.DisposeIfUsingInternalTransaction(appContext.IsUsingExternalTransaction);
            dbConnection.DisposeIfUsingInternalTransaction();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}