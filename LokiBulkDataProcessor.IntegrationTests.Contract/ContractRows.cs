using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using FastMember;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels.Dtos;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestObjectBuilders;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// Row sets shared by every provider's contract run, in every input shape Loki accepts (models, DataTable,
/// IDataReader, JSON). No value is an empty string: Oracle stores <c>""</c> as NULL, so that case has its own
/// Oracle-only test. No row set carries an identity column (Blogs.Id, Posts.Id, TestDbModels.Id): the contract never
/// writes identity columns, and row identity is the values, never a generated Id.
/// </summary>
public static class ContractRows
{
    public static readonly string[] TestDbModelColumns =
        { "StringColumn", "DateColumn", "BoolColumn", "NullableBoolColumn", "NullableDateColumn" };

    public static readonly DateTime Date1 = new(2020, 01, 26);
    public static readonly DateTime Date2 = new(2020, 01, 27);
    public static readonly DateTime Date3 = new(2020, 01, 28);

    /// <summary>A TestDbModel with every non-nullable column set and both nullable columns null.</summary>
    public static TestDbModel TestDbModel(string value, DateTime? date = null, bool flag = true) =>
        TestObjectFactory.TestDbModelObject()
            .WithStringColumnValue(value)
            .WithDateColumnValue(date ?? Date1)
            .WithBoolColumnValue(flag)
            .Build();

    /// <summary>A TestDbModel with every column, including the nullable ones, set.</summary>
    public static TestDbModel FullTestDbModel(string value, DateTime date, bool flag, bool? nullableFlag, DateTime? nullableDate)
    {
        TestDbModel model = TestDbModel(value, date, flag);
        model.NullableBoolColumn = nullableFlag;
        model.NullableDateColumn = nullableDate;
        return model;
    }

    public static TestDbModel[] TwoTestDbModels() => new[] { TestDbModel("One"), TestDbModel("Two", Date2, flag: false) };

    /// <summary>Two rows that mix null and non-null values in the nullable columns.</summary>
    public static TestDbModel[] TwoMixedNullTestDbModels() => new[]
    {
        FullTestDbModel("String Value 1", Date1, true, null, null),
        FullTestDbModel("String Value 2", Date2, false, true, new DateTime(2020, 01, 19))
    };

    public static TestDbModel[] ThreeMixedNullTestDbModels() =>
        TwoMixedNullTestDbModels().Append(FullTestDbModel("String Value 3", Date3, true, false, new DateTime(2020, 1, 10))).ToArray();

    public static DataTable TestDbModelsDataTable(params TestDbModel[] rows)
    {
        TestDataTableBuilder builder = TestObjectFactory.NewTestDataTable();
        foreach (TestDbModel row in rows)
        {
            builder.WithRowData(row.StringColumn, row.BoolColumn, row.DateColumn, row.NullableBoolColumn, row.NullableDateColumn);
        }

        return builder.Build();
    }

    public static IDataReader TestDbModelsReader(params TestDbModel[] rows) => ObjectReader.Create(rows, TestDbModelColumns);

    /// <summary>A Loki JSON payload for the TestDbModels columns.</summary>
    public static Stream TestDbModelsJson(string tableName, params TestDbModel[] rows) => Utf8Json(new
    {
        tableName,
        columns = new[]
        {
            Column("StringColumn", "string"), Column("DateColumn", "datetime"),
            Column("BoolColumn", "bool"), Column("NullableBoolColumn", "bool"), Column("NullableDateColumn", "datetime")
        },
        data = rows.Select(row => new
        {
            row.StringColumn, row.DateColumn, row.BoolColumn, row.NullableBoolColumn, row.NullableDateColumn
        })
    });

    /// <summary>A Loki JSON payload with PostDataMapping's source columns (ATitle, ContentA, ABlogId), so it only loads through that mapping.</summary>
    public static Stream MappedPostsJson(string tableName, int blogId, params string[] titles) => Utf8Json(new
    {
        tableName,
        columns = new[] { Column("ATitle", "string"), Column("ContentA", "string"), Column("ABlogId", "int") },
        data = titles.Select(title => new { ATitle = title, ContentA = $"{title} content", ABlogId = blogId })
    });

    public static Post[] Posts(int blogId, int count = 2) =>
        Enumerable.Range(1, count)
            .Select(i => new Post { Title = $"Post{i}", Content = $"Post {i} content", BlogId = blogId })
            .ToArray();

    public static PostDto[] PostDtos(int blogId) => new[]
    {
        TestObjectFactory.NewPostDto().WithTitle("Post1").WithContent("Post 1 content").WithBlogId(blogId).Build(),
        TestObjectFactory.NewPostDto().WithTitle("Post2").WithContent("Post 2 content").WithBlogId(blogId).Build()
    };

    public static PostDtoWithNoMapping[] UnmappedPostDtos(int blogId) => new[]
    {
        new PostDtoWithNoMappingBuilder().WithTitle("Post1").WithContent("Post 1 content").WithBlogId(blogId).Build(),
        new PostDtoWithNoMappingBuilder().WithTitle("Post2").WithContent("Post 2 content").WithBlogId(blogId).Build()
    };

    /// <summary>Two posts (Post1/Content1, Post2/Content2) in a DataTable with the given name and column names.</summary>
    public static DataTable PostsDataTable(string tableName, string titleColumn, string contentColumn, string blogIdColumn, int blogId) =>
        TestObjectFactory.NewPostDataTable()
            .Create()
            .WithTableName(tableName)
            .WithCustomColumnNames(titleColumn, contentColumn, blogIdColumn)
            .WithRowData(blogId, "Post1", "Content1")
            .WithRowData(blogId, "Post2", "Content2")
            .Build();

    /// <summary>One post in an IDataReader with the given column names.</summary>
    public static IDataReader PostReader(string titleColumn, string contentColumn, string blogIdColumn, int blogId, string title)
    {
        DataTable table = TestObjectFactory.NewPostDataTable().Create()
            .WithCustomColumnNames(titleColumn, contentColumn, blogIdColumn)
            .WithRowData(blogId, title, $"{title} content")
            .Build();
        return table.CreateDataReader();
    }

    private static object Column(string name, string type) => new { name, type };

    private static Stream Utf8Json(object payload) => new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(payload));
}
