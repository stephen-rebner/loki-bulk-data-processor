using LokiBulkDataProcessor.IntegrationTests.TestModels;
using System;

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public class ColsInDiffOrderObjectBuilder
    {
        private int _id;
        private string _stringColumn;
        private bool _boolColumn;
        private DateTime _dateColumn;
        private bool? _nullableBoolColumn;
        private DateTime? _nullableDateTimeColumn;

        public ColsInDiffOrderObjectBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public ColsInDiffOrderObjectBuilder WithStringColumnValue(string stringColumnValue)
        {
            _stringColumn = stringColumnValue;
            return this;
        }

        public ColsInDiffOrderObjectBuilder WithBoolColumnValue(bool boolColumnValue)
        {
            _boolColumn = boolColumnValue;
            return this;
        }

        public ColsInDiffOrderObjectBuilder WithDateColumnValue(DateTime dateColumnValue)
        {
            _dateColumn = dateColumnValue;
            return this;
        }

        public ColsInDiffOrderObjectBuilder WithNullableBoolColumnValue(bool? boolColumnValue)
        {
            _nullableBoolColumn = boolColumnValue;
            return this;
        }

        public ColsInDiffOrderObjectBuilder WithNullableDateColumnValue(DateTime? dateColumnValue)
        {
            _nullableDateTimeColumn = dateColumnValue;
            return this;
        }

        public ColsInDiffOrderObject Build()
        {
            return new ColsInDiffOrderObject
            {
                Id = _id,
                NullableDateColumn = _nullableDateTimeColumn,
                StringColumn = _stringColumn,
                BoolColumn = _boolColumn,
                DateColumn = _dateColumn,
                NullableBoolColumn = _nullableBoolColumn
            };
        }
    }
}
