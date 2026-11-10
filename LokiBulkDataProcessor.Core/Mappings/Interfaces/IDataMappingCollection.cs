namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    internal interface IDataMappingCollection
    {
        DataMapping GetMappingFor(string tableName);
    }
}
