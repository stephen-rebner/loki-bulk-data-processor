using System.Data;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IBulkCopyDataTableCommand : IBulkCopyCommand
    {
        public DataTable DataToCopy { get; set; }
    }
}
