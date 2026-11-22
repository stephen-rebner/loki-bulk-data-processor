namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    /// <summary>The first step of a <see cref="DataMapping"/>: choose a source column.</summary>
    public interface IMapDataTableSource
    {
        /// <summary>Maps the source column <paramref name="sourceColumnName"/>; follow it with <see cref="IToDestination.ToDestinationColumn"/>.</summary>
        /// <param name="sourceColumnName">The column name in the data being copied.</param>
        /// <returns>The step that names the destination column.</returns>
        IToDestination Map(string sourceColumnName);
    }
}
