using Loki.BulkDataProcessor.Core.Mappings;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class DataMappingWithDuplicateSourceColumn : DataMapping
    {
        public override string SourceTableName => "DataMappingWithEmptyDestCol";

        public DataMappingWithDuplicateSourceColumn()
        {
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn("public_string");
            Map("PublicString").ToDestinationColumn("adsf");
        }
    }
}
