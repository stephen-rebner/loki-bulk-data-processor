namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IAsPrimaryKey
    {
        /// <summary>
        /// Maps the object property / in-memory datatable column as the primary key
        /// </summary>
        void AsPrimaryKey();
    }
}
