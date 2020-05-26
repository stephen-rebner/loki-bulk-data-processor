namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IMapDataTableSource
    {
        IToDestination Map(string sourceColumnName);
    }
}
