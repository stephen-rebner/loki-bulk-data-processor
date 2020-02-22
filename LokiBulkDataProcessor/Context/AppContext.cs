using System;
using System.Collections.Generic;
using System.Text;

namespace Loki.BulkDataProcessor.Context
{
    public interface AppContext
    {
        public string ConnectionString { get; set; }
        public int Timeout { get; set; }
        public int BatchSize { get;set;}
    }
}
