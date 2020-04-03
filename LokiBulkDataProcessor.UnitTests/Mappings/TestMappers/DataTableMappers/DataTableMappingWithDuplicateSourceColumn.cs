using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.UnitTests.TestModels;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class DataTableMappingWithDuplicateSourceColumn : AbstractDataTableMapper
    {
        public DataTableMappingWithDuplicateSourceColumn()
        {
            ForDataTable("DataTableMappingWithEmptyDestCol");
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn("public_string");
            Map("PublicString").ToDestinationColumn("adsf");
        }
    }

}
