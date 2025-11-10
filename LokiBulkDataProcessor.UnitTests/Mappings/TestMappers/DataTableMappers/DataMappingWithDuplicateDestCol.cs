using Loki.BulkDataProcessor.Core.Mappings;


namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class DataMappingWithDuplicateDestCol : DataMapping
    {
        public override string SourceTableName => "DataMappingWithDuplicateDestCol";

        public DataMappingWithDuplicateDestCol()
        {
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn("public_string");
            Map("PublicString1").ToDestinationColumn("public_string");
        }
    }
}
