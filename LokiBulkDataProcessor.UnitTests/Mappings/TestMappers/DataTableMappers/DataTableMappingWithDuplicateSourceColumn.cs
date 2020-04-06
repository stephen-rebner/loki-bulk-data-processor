using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class DataTableMappingWithDuplicateSourceColumn : DataTableMapping
    {
        public override string SourceTableName => "DataTableMappingWithEmptyDestCol";

        public DataTableMappingWithDuplicateSourceColumn()
        {
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn("public_string");
            Map("PublicString").ToDestinationColumn("adsf");
        }
    }
}
