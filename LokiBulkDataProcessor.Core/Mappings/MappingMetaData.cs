namespace Loki.BulkDataProcessor.Core.Mappings
{
    internal class MappingMetaData
    {
        internal string? SourceColumn { get; set; }
        
        internal string? DestinationColumn { get; set; }

        internal bool IsPrimaryKey { get; set; }
    }
}
