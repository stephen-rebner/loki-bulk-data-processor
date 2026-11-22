using Loki.BulkDataProcessor.Mappings;
using Loki.Samples.WebApi.Engines;

namespace Loki.Samples.WebApi.Mappings;

// The mappings of the JSON upload. JSON saves look a DataMapping up by the destination table, and with
// RequireDataMappingForJson = true (Program.cs) a JSON save without one fails before connecting. Each mapping lists the only
// columns a payload can write: a payload column that is not mapped here is never copied.

/// <summary>The columns a JSON upload may write to Customers (never Id).</summary>
internal sealed class CustomersJsonMapping : DataMapping
{
    public override string SourceTableName => ImportTables.Customers;

    public CustomersJsonMapping()
    {
        Map("Name").ToDestinationColumn("Name");
        Map("Email").ToDestinationColumn("Email");
        Map("CreatedOn").ToDestinationColumn("CreatedOn");
    }
}

/// <summary>The columns a JSON upload may write to Products (never Id).</summary>
internal sealed class ProductsJsonMapping : DataMapping
{
    public override string SourceTableName => ImportTables.Products;

    public ProductsJsonMapping()
    {
        Map("Sku").ToDestinationColumn("Sku");
        Map("Name").ToDestinationColumn("Name");
        Map("Price").ToDestinationColumn("Price");
    }
}
