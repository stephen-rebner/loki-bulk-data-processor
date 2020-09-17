using System.Data;

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public class PostDataTableBuilder
    {
        private string Id = "Id";
        private string BlogId = "BlogId";
        private string Title = "Title";
        private string Content = "Content";

        private DataTable _dataTable;

        public PostDataTableBuilder Create()
        {
            _dataTable = new DataTable();
            return this;
        }

        public PostDataTableBuilder WithDefaultColumnNames()
        {
            _dataTable.Columns.Add(new DataColumn(BlogId));
            _dataTable.Columns.Add(new DataColumn(Title));
            _dataTable.Columns.Add(new DataColumn(Content));

            return this;
        }

        public PostDataTableBuilder WithDefaultPrimaryKey()
        {
            _dataTable.Columns.Add(new DataColumn(Id));

            return this;
        }

        public PostDataTableBuilder WithCustomPrimaryKey(string primaryKey)
        {
            Id = primaryKey;

            _dataTable.Columns.Add(new DataColumn(primaryKey));

            return this;
        }

        public PostDataTableBuilder WithCustomColumnNames(string titleColumnName, string contentColumnName, string blogIdColumnName)
        {
            Title = titleColumnName;
            Content = contentColumnName;
            BlogId = blogIdColumnName;

            _dataTable.Columns.Add(new DataColumn(BlogId));
            _dataTable.Columns.Add(new DataColumn(Title));
            _dataTable.Columns.Add(new DataColumn(Content));

            return this;
        }

        public PostDataTableBuilder WithTableName(string tableName)
        {
            _dataTable.TableName = tableName;

            return this;
        }

        public PostDataTableBuilder WithRowData(
            int blogId,
            string title,
            string content)
        {
            var dataRow = _dataTable.NewRow();
            dataRow[BlogId] = blogId;
            dataRow[Title] = title;
            dataRow[Content] = content;

            _dataTable.Rows.Add(dataRow);
            return this;
        }

        public PostDataTableBuilder WithPrimaryKeyValue(int primaryKey, int rowIndex)
        {
            _dataTable.Rows[rowIndex].SetField(Id, primaryKey);

            return this;
        }

        public DataTable Build()
        {
            return _dataTable;
        }
    }
}
