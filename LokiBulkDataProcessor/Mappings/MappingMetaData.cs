﻿namespace Loki.BulkDataProcessor.Mappings
{
    internal class MappingMetaData
    {
        internal string SourceColumn { get; set; }
        
        internal string DestinationColumn { get; set; }

        internal bool IsIdentityColumn { get; set; }
    }
}
