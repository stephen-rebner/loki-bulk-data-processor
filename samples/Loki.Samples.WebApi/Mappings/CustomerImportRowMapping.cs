using Loki.BulkDataProcessor.Mappings;
using Loki.Samples.WebApi.Imports;

namespace Loki.Samples.WebApi.Mappings;

/// <summary>
/// Applies to every save of <c>IEnumerable&lt;CustomerImportRow&gt;</c>. Renames the properties to the Customers columns, and limits
/// the copy to them: Customers.Id is an identity column (Oracle: GENERATED ALWAYS) and must not be written.
/// </summary>
internal sealed class CustomerImportRowMapping : ModelMapping<CustomerImportRow>
{
    public CustomerImportRowMapping()
    {
        Map(row => row.FullName).ToDestinationColumn("Name");
        Map(row => row.EmailAddress).ToDestinationColumn("Email");
        Map(row => row.RegisteredOn).ToDestinationColumn("CreatedOn");
    }
}
