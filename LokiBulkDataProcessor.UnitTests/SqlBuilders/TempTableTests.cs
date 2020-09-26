using Loki.BulkDataProcessor.Constants;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace LokiBulkDataProcessor.UnitTests.SqlBuilders
{
    public class TempTableTests
    {
        private DataTable _destinationTableInfo;

        [SetUp]
        public void SetUp()
        {
            _destinationTableInfo = new DataTable();

            _destinationTableInfo.Columns.Add(new DataColumn(DestTableInfoColumns.COLUMN_NAME));
            _destinationTableInfo.Columns.Add(new DataColumn(DestTableInfoColumns.DATA_TYPE));
            _destinationTableInfo.Columns.Add(new DataColumn(DestTableInfoColumns.CONSTRAINT_TYPE));
        }

        [Test]
        public void GenerateCreateStatement_ShouldCreateValidCreateTableSql()
        {
            GivenDestinationTableInfoWithDataTypes("VARCHAR", "NVARCHAR", "BIT");


        }

        private void GivenDestinationTableInfoWithDataTypes(params string[] dataTypes)
        {
            foreach(var dataType in dataTypes)
            {

            }
        }
    }
}
