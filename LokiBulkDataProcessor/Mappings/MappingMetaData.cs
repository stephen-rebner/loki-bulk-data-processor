using System;
using System.Collections.Generic;
using System.Text;

namespace Loki.BulkDataProcessor.Mappings
{
    internal class MappingMetaData
    {
        internal string SourceColumn { get; set; }
        
        internal string DestinationColumn { get; set; }

        internal bool IsPrimaryKey { get; set; }
    }
}
