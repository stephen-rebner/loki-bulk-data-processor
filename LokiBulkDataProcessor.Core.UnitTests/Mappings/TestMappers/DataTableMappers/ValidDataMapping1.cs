using Loki.BulkDataProcessor.Core.Mappings;

namespace LokiBulkDataProcessor.Core.UnitTests.Mappings.TestMappers
{
    public class ValidDataMapping1 : DataMapping
    {
        public override string SourceTableName => "ValidDataMapping1";

        public ValidDataMapping1()
        {
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn("public_string");
        }
    }
}
