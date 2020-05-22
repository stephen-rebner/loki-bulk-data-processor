using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IBulkProcessorModelsCommand<T> : IBulkProcessorCommand where T : class
    {
        public IEnumerable<T> DataToCopy { get; set; }
    }
}
