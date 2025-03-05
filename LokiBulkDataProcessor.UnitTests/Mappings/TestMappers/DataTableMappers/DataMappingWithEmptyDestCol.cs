﻿using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class DataMappingWithEmptyDestCol : DataMapping
    {
        public override string SourceTableName => "DataMappingWithEmptyDestCol";

        public DataMappingWithEmptyDestCol()
        {
            Map("PublicInt").ToDestinationColumn("public_int");
            Map("PublicBool").ToDestinationColumn("public_bool");
            Map("BaseInt").ToDestinationColumn("base_int");
            Map("PublicString").ToDestinationColumn(" ");
        }
    }
}
