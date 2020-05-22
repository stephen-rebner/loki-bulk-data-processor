using System.Data;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IBulkProcessorDataTableCommand : IBulkProcessorCommand
    {
        public DataTable DataToCopy { get; set; }
    }
}
