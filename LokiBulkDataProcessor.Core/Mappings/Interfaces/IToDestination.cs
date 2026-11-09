namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IToDestination
    {
        /// <summary>
        /// Determines the database column name to map as the destination
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        void ToDestinationColumn(string destinationColumnName);
    }
}
