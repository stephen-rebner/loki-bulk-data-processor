﻿using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.UnitTests.TestModels;

namespace LokiBulkDataProcessor.UnitTests.Mappings.TestMappers
{
    public class TestMapping2 : AbstractModelMapping<ValidModelObjectB>
    {
        public TestMapping2()
        {
            Map(o => o.BaseInt).ToDestinationColumn("base_int");
            Map(o => o.AnotherColumn).ToDestinationColumn("another_column");
        }
    }
}
