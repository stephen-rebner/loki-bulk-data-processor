namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    /// <summary>Reserved: primary-key mapping is not implemented and no mapping step returns this interface.</summary>
    public interface IAsPrimaryKey
    {
        /// <summary>
        /// Maps the object property / in-memory datatable column as the primary key
        /// </summary>
        void AsPrimaryKey();
    }
}
