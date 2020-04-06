using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class ValidDataTableMapping1 : AbstractDataTableMapper
    {
        public override string SourceTableName => throw new System.NotImplementedException();

        public ValidDataTableMapping1()
        {
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn("public_string");
        }
    }
}
