using System;
using System.Data;

namespace LokiBulkDataProcessor.IntegrationTests.Contract.TestObjectBuilders
{
    public class TestDataTableBuilder
    {
        private const string StringColumnHeader = "StringColumn";
        private const string BoolColumnHeader = "BoolColumn";
        private const string DateColumnHeader = "DateColumn";
        private const string NullableBoolColumnHeader = "NullableBoolColumn";
        private const string NullableDateColumnHeader = "NullableDateColumn";

        private readonly DataTable _dataTable; 

        public TestDataTableBuilder()
        {
            // Typed columns: an untyped DataColumn is a string column, and "True" or a culture-formatted date is converted
            // by SqlBulkCopy but not by Oracle (ORA-01722 for NUMBER(1), NLS-dependent for TIMESTAMP).
            _dataTable = new DataTable();
            _dataTable.Columns.Add(new DataColumn(StringColumnHeader, typeof(string)));
            _dataTable.Columns.Add(new DataColumn(BoolColumnHeader, typeof(bool)));
            _dataTable.Columns.Add(new DataColumn(DateColumnHeader, typeof(DateTime)));
            _dataTable.Columns.Add(new DataColumn(NullableBoolColumnHeader, typeof(bool)));
            _dataTable.Columns.Add(new DataColumn(NullableDateColumnHeader, typeof(DateTime)));
        }

        public TestDataTableBuilder WithRowData(
            string stringValue,
            bool boolValue,
            DateTime dateValue,
            bool? nullableBoolValue,
            DateTime? nullableDateValue)
        {
            var dataRow = _dataTable.NewRow();
            dataRow[StringColumnHeader] = (object)stringValue ?? DBNull.Value;
            dataRow[BoolColumnHeader] = boolValue;
            dataRow[DateColumnHeader] = dateValue;
            dataRow[NullableBoolColumnHeader] = (object)nullableBoolValue ?? DBNull.Value;
            dataRow[NullableDateColumnHeader] = (object)nullableDateValue ?? DBNull.Value;

            _dataTable.Rows.Add(dataRow);
            return this;
        }

        public DataTable Build()
        {
            return _dataTable;
        }
    }
}
