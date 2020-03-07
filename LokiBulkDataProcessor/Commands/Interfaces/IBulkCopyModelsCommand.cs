using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface IBulkCopyModelsCommand<T> : IBulkCopyCommand where T : class
    {
        public IEnumerable<T> DataToCopy { get; set; }
    }
}
