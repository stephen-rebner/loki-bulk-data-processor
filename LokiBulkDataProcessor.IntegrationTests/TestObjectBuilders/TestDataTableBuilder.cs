using System;
using System.Data;

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public class TestDataTableBuilder
    {
        private const string IdColumnHeader = "Id";
        private const string StringColumnHeader = "StringColumn";
        private const string BoolColumnHeader = "BoolColumn";
        private const string DateColumnHeader = "DateColumn";
        private const string NullableBoolColumnHeader = "NullableBoolColumn";
        private const string NullableDateColumnHeader = "NullableDateColumn";

        private readonly DataTable _dataTable; 

        public TestDataTableBuilder()
        {
            _dataTable = new DataTable();
            _dataTable.Columns.Add(new DataColumn(StringColumnHeader));
            _dataTable.Columns.Add(new DataColumn(BoolColumnHeader));
            _dataTable.Columns.Add(new DataColumn(DateColumnHeader));
            _dataTable.Columns.Add(new DataColumn(NullableBoolColumnHeader));
            _dataTable.Columns.Add(new DataColumn(NullableDateColumnHeader));
            _dataTable.Columns.Add(new DataColumn(IdColumnHeader));
        }

        public TestDataTableBuilder WithRowData(
            int id,
            string stringValue,
            bool boolValue,
            DateTime dateValue,
            bool? nullableBoolValue,
            DateTime? nullableDateValue)
        {
            var dataRow = _dataTable.NewRow();
            dataRow[IdColumnHeader] = id;
            dataRow[StringColumnHeader] = stringValue;
            dataRow[BoolColumnHeader] = boolValue;
            dataRow[DateColumnHeader] = dateValue;
            dataRow[NullableBoolColumnHeader] = nullableBoolValue;
            dataRow[NullableDateColumnHeader] = nullableDateValue;

            _dataTable.Rows.Add(dataRow);
            return this;
        }

        public DataTable Build()
        {
            return _dataTable;
        }
    }
}
