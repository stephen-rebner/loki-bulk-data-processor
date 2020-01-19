using System;

namespace LokiBulkDataProcessor.IntegrationTests.TestModel
{
    public class TestDbModel
    {
        public int Id { get; set; }
        public string StringColumn { get; set; }
        public bool BoolColumn { get; set; }
        public DateTime DateColumn { get; set; }
    }
}
