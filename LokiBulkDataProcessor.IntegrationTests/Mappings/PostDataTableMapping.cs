﻿using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.IntegrationTests.Mappings
{
    public class PostDataTableMapping : DataTableMapping
    {
        public override string SourceTableName => "Posts";

        public PostDataTableMapping()
        {
            Map("Id").ToDestinationColumn("Id").AsPrimaryKey();
            Map("ATitle").ToDestinationColumn("Title");
            Map("ContentA").ToDestinationColumn("Content");
            Map("ABlogId").ToDestinationColumn("BlogId");
        }
    }
}
