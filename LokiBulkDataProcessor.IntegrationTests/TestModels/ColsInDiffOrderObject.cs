using System;

namespace LokiBulkDataProcessor.IntegrationTests.TestModels
{
    public class TestDbModel
    {
        public string StringColumn { get; set; }

        public bool BoolColumn { get; set; }

        public DateTime DateColumn { get; set; }

        public bool? NullableBoolColumn { get; set; }

        public DateTime? NullableDateColumn { get; set; }

        public int Id { get; set; }
        
    }
}
