namespace Loki.BulkDataProcessor.Core.Mappings.Interfaces
{
    public interface IDataMappingCollection
    {
        DataMapping GetMappingFor(string tableName);
    }
}
