using FluentAssertions;
using Loki.BulkDataProcessor.SqlBuilders;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.SqlBuilders
{
    public class SchemaQueryTests
    {
        [Test]
        public void GenerateDataTableInfoQuery_ShouldGenerateQueryCorrectly_WhenTableNameDoesNotContainSchema()
        {
            var tableName = "A_Table";
            var databaseName = "A_Database";

            var expectedQuery = @$"SELECT 
                    c.COLUMN_NAME, DATA_TYPE, tc.CONSTRAINT_TYPE
                    FROM INFORMATION_SCHEMA.COLUMNS c 
                    left join INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE cu on cu.COLUMN_NAME = c.COLUMN_NAME 
                    and cu.TABLE_NAME = '{ tableName }' and cu.TABLE_SCHEMA = 'dbo'
                    left join INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc on tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME
                    WHERE c.TABLE_NAME = '{ tableName }' and c.TABLE_CATALOG = '{ databaseName }' and c.TABLE_SCHEMA = 'dbo'
                    ORDER BY ORDINAL_POSITION";

            var actualQuery = TableInfo.GenerateDatabaseTableInfoQuery(tableName, databaseName);

            actualQuery.Should().Be(expectedQuery);
        }

        [Test]
        public void GenerateDataTableInfoQuery_ShouldGenerateQueryCorrectly_WhenTableNameContainsSchema()
        {
            var schemaName = "A_Schema";
            var tableName = "A_Table";
            var databaseName = "A_Database";

            var expectedQuery = @$"SELECT 
                    c.COLUMN_NAME, DATA_TYPE, tc.CONSTRAINT_TYPE
                    FROM INFORMATION_SCHEMA.COLUMNS c 
                    left join INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE cu on cu.COLUMN_NAME = c.COLUMN_NAME 
                    and cu.TABLE_NAME = '{ tableName }' and cu.TABLE_SCHEMA = '{ schemaName }'
                    left join INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc on tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME
                    WHERE c.TABLE_NAME = '{ tableName }' and c.TABLE_CATALOG = '{ databaseName }' and c.TABLE_SCHEMA = '{ schemaName }'
                    ORDER BY ORDINAL_POSITION";

            var actualQuery = TableInfo.GenerateDatabaseTableInfoQuery(string.Concat(schemaName, ".", tableName), databaseName);

            actualQuery.Should().Be(expectedQuery);
        }
    }
}
