using System.Data;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands.Interfaces;

public interface IBulkCopyFromDataReaderCommand
{
    Task Execute(IDataReader dataReader, string destinationTableName);
}