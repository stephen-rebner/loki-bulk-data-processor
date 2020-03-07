using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IBulkCopyCommand
    {
        int BatchSize { get; set; }

        int Timeout { get; set; }

        string TableName { get; set; }

        string ConnectionString { get; set; }

        Task Execute();
    }
}
