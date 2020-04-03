using System.Data;

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public class PostDataTableBuilder
    {
        private const string BlogId = "BlogId";
        private const string Title = "Title";
        private const string Content = "Content";

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

        public PostDataTableBuilder WithCustomColumnNames(string titleColumnName, string contentColumnName, string blogIdColumnName)
        {
            _dataTable.Columns.Add(new DataColumn(blogIdColumnName));
            _dataTable.Columns.Add(new DataColumn(titleColumnName));
            _dataTable.Columns.Add(new DataColumn(contentColumnName));

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
            dataRow[0] = blogId;
            dataRow[1] = title;
            dataRow[2] = content;

            _dataTable.Rows.Add(dataRow);
            return this;
        }

        public DataTable Build()
        {
            return _dataTable;
        }
    }
}
