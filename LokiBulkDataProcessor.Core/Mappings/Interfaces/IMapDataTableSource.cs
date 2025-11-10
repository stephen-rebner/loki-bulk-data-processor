namespace Loki.BulkDataProcessor.Core.Mappings.Interfaces
{
    public interface IMapDataTableSource
    {
        IToDestination Map(string sourceColumnName);
    }
}
