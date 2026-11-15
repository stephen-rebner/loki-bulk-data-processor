using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using System;

namespace LokiBulkDataProcessor.IntegrationTests.Contract.TestObjectBuilders
{
    public class TestDbModelBuilder
    {
        private string _stringColumn;
        private bool _boolColumn;
        private DateTime _dateColumn;
        private bool? _nullableBoolColumn;
        private DateTime? _nullableDateTimeColumn;

        public TestDbModelBuilder WithStringColumnValue(string stringColumnValue)
        {
            _stringColumn = stringColumnValue;
            return this;
        }

        public TestDbModelBuilder WithBoolColumnValue(bool boolColumnValue)
        {
            _boolColumn = boolColumnValue;
            return this;
        }

        public TestDbModelBuilder WithDateColumnValue(DateTime dateColumnValue)
        {
            _dateColumn = dateColumnValue;
            return this;
        }

        public TestDbModelBuilder WithNullableBoolColumnValue(bool? boolColumnValue)
        {
            _nullableBoolColumn = boolColumnValue;
            return this;
        }

        public TestDbModelBuilder WithNullableDateColumnValue(DateTime? dateColumnValue)
        {
            _nullableDateTimeColumn = dateColumnValue;
            return this;
        }

        public TestDbModel Build()
        {
            return new TestDbModel
            {
                NullableDateColumn = _nullableDateTimeColumn,
                StringColumn = _stringColumn,
                BoolColumn = _boolColumn,
                DateColumn = _dateColumn,
                NullableBoolColumn = _nullableBoolColumn
            };
        }
    }
}
