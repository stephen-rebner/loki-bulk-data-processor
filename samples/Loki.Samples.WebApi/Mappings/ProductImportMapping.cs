using Loki.BulkDataProcessor.Mappings;

namespace Loki.Samples.WebApi.Mappings;

/// <summary>
/// Applies when the source name is "ProductImport": a <c>DataTable</c> whose <c>TableName</c> is "ProductImport", or a reader saved
/// with <c>SaveAsync(reader, "Products", sourceName: "ProductImport")</c>. The destination table does not matter for the lookup.
/// </summary>
internal sealed class ProductImportMapping : DataMapping
{
    public const string Source = "ProductImport";

    public override string SourceTableName => Source;

    public ProductImportMapping()
    {
        Map("ImportSku").ToDestinationColumn("Sku");
        Map("ImportName").ToDestinationColumn("Name");
        Map("ImportPrice").ToDestinationColumn("Price");
    }
}
