using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class DataTableMappingWithDuplicateDestCol : AbstractDataTableMapper
    {
        public DataTableMappingWithDuplicateDestCol()
        {
            ForDataTable("DataTableMappingWithDuplicateDestCol");
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn("public_string");
            Map("PublicString1").ToDestinationColumn("public_string");
        }
    }

}
