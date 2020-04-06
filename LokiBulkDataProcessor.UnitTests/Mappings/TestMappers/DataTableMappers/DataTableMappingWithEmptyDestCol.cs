using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class DataTableMappingWithEmptyDestCol : DataTableMapping
    {
        public override string SourceTableName => "DataTableMappingWithEmptyDestCol";

        public DataTableMappingWithEmptyDestCol()
        {
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn(" ");
        }
    }
}
