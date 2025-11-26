using System;
using System.Collections.Generic;

namespace LokiBulkDataProcessor.IntegrationTests.TestModels
{
    public class Blog
    {
        public int Id { get; set; }

        public string Url { get; set; }

        public List<Post> Posts { get; set; }
    }
}
