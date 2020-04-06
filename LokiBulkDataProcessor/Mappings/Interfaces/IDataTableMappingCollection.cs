namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IDataTableMappingCollection
    {
        DataTableMapping GetMappingFor(string tableName);
    }
}
