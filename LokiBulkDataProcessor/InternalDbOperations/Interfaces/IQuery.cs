using System;
using System.Data;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    internal interface IQuery : IDisposable
    {
        DataTable Load(string queryText);
    }
}
