using System.Data;

namespace LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders
{
    public class PostDataTableBuilder
    {
        private const string BlogId = "blogId";
        private const string Title = "Title";
        private const string Content = "Content";

        private readonly DataTable _dataTable; 

        public PostDataTableBuilder()
        {
            _dataTable = new DataTable();
            _dataTable.Columns.Add(new DataColumn(BlogId));
            _dataTable.Columns.Add(new DataColumn(Title));
            _dataTable.Columns.Add(new DataColumn(Content));
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

        public DataTable Build()
        {
            return _dataTable;
        }
    }
}
