namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    /// <summary>The second step of a mapping: name the destination column for the source chosen by <c>Map</c>.</summary>
    public interface IToDestination
    {
        /// <summary>
        /// Determines the database column name to map as the destination
        /// </summary>
        /// <param name="destinationColumnName">The column in the destination table. Each destination column may be mapped once.</param>
        void ToDestinationColumn(string destinationColumnName);
    }
}
