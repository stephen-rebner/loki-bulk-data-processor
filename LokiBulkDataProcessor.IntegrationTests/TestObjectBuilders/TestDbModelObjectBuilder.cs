using LokiBulkDataProcessor.IntegrationTests.TestModel;
using System;

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public class TestDbModelObjectBuilder
    {
        private int _id;
        private string _stringColumn;
        private bool _boolColumn;
        private DateTime _dateColumn;
        private bool? _nullableBoolColumn;
        private DateTime? _nullableDateTimeColumn;

        public TestDbModelObjectBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public TestDbModelObjectBuilder WithStringColumnValue(string stringColumnValue)
        {
            _stringColumn = stringColumnValue;
            return this;
        }

        public TestDbModelObjectBuilder WithBoolColumnValue(bool boolColumnValue)
        {
            _boolColumn = boolColumnValue;
            return this;
        }

        public TestDbModelObjectBuilder WithDateColumnValue(DateTime dateColumnValue)
        {
            _dateColumn = dateColumnValue;
            return this;
        }

        public TestDbModelObjectBuilder WithNullableBoolColumnValue(bool? boolColumnValue)
        {
            _nullableBoolColumn = boolColumnValue;
            return this;
        }

        public TestDbModelObjectBuilder WithNullableDateColumnValue(DateTime? dateColumnValue)
        {
            _nullableDateTimeColumn = dateColumnValue;
            return this;
        }

        public TestDbModel Build()
        {
            return new TestDbModel
            {
                Id = _id,
                StringColumn = _stringColumn,
                BoolColumn = _boolColumn,
                DateColumn = _dateColumn,
                NullableBoolColumn = _nullableBoolColumn,
                NullableDateColumn = _nullableDateTimeColumn
            };
        }
    }
}
