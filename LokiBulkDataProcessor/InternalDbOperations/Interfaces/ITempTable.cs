using System;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    public interface ITempTable
    {
        /// <summary>
        /// Creates a Temp Table based on the data model Type
        /// </summary>
        void Create(Type modelType);
        void Drop();
    }
}
