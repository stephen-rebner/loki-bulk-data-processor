using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class DataTableMappingWithNullDestCol : AbstractDataTableMapper
    {
        public DataTableMappingWithNullDestCol()
        {
            ForDataTable("DataTableMappingWithNullDestCol");
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn(null);
        }
    }

}
