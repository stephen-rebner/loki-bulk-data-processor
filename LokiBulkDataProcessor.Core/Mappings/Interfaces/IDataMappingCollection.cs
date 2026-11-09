namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IDataMappingCollection
    {
        DataMapping GetMappingFor(string tableName);
    }
}
