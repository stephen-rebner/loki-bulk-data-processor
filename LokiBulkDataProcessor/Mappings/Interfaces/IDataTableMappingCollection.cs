namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IDataTableMappingCollection
    {
        AbstractDataTableMapper GetMappingFor(string tableName);
    }
}
