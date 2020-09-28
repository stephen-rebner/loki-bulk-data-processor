using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    internal interface ISqlDbTransaction : IDbTransaction
    {
        void DisposeIfUsingInternalTransaction();
    }
}
