using Loki.BulkDataProcessor.Core.Mappings;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class DataMappingWithNullDestCol : DataMapping
    {
        public override string SourceTableName => "DataMappingWithNullDestCol";

        public DataMappingWithNullDestCol()
        {
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn(null);
        }
    }
}
