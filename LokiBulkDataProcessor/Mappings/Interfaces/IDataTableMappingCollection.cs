namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IDataTableMappingCollection
    {
        AbstractDataTableMapping GetMappingFor(string tableName);
    }
}
