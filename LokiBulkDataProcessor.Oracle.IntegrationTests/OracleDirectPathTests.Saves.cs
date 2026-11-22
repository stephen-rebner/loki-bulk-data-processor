using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using LokiBulkDataProcessor.IntegrationTests.Contract;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using NUnit.Framework;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// Successful direct-path loads in Loki's own transaction, for every input shape, with BatchSize smaller than the row count, and how
/// <see cref="bool"/> values reach <c>NUMBER(1)</c> and <c>BOOLEAN</c> columns (ODP.NET converts them; BooleanBinding does not apply).
/// </summary>
public sealed partial class OracleDirectPathTests
{
    private const string SelectFlags = "SELECT Label, NumberFlag, BooleanFlag FROM OracleTypes ORDER BY Label";

    public enum Shape
    {
        Models,
        DataTable,
        DataReader,
        Json
    }

    /// <summary>TestDbModels has two NUMBER(1) columns, so this also proves the driver converts bool (and a null bool?) to NUMBER(1).</summary>
    [TestCase(Shape.Models)]
    [TestCase(Shape.DataTable)]
    [TestCase(Shape.DataReader)]
    [TestCase(Shape.Json)]
    public async Task SaveAsync_ShouldLoadEveryRowWithBoolsAndNulls_WhenSavingEachInputShape(Shape shape)
    {
        TestDbModel[] rows = ContractRows.ThreeMixedNullTestDbModels();

        await SaveTestDbModelsAsync(Processor(), shape, rows);

        (await _database.ReadTestDbModelsAsync()).Should().BeEquivalentTo(rows);
    }

    [Test]
    public async Task SaveAsync_ShouldLoadEveryRow_WhenBatchSizeIsSmallerThanTheRowCount()
    {
        TestDbModel[] rows = Enumerable.Range(1, 10).Select(i => ContractRows.TestDbModel($"Row{i:D2}", flag: i % 2 == 0)).ToArray();
        IBulkProcessor processor = Processor();
        processor.BatchSize = 3;

        await processor.SaveAsync(rows, TestDbModelsTable);

        (await _database.ReadTestDbModelsAsync()).Should().BeEquivalentTo(rows);
    }

    [Test]
    public async Task SaveAsync_ShouldLoadThePostsThroughTheModelMapping_WhenSavingPostModels()
    {
        int blogId = await _database.InsertBlogAsync("http://a-url.com");

        await Processor().SaveAsync(ContractRows.Posts(blogId), "Posts");

        (await _database.ReadPostsAsync()).Select(post => (post.BlogId, post.Title, post.Content))
            .Should().BeEquivalentTo(new[] { (blogId, "Post1", "Post 1 content"), (blogId, "Post2", "Post 2 content") });
    }

    /// <summary>Records how ODP.NET's direct path converts bool: 1/0/NULL into NUMBER(1) and TRUE/FALSE/NULL into a 23ai BOOLEAN.</summary>
    [Test]
    public async Task SaveAsync_ShouldStoreBoolsAsOneZeroAndBoolean_WhenTheColumnsAreNumberAndBoolean()
    {
        using DataTable rows = Table(("Label", typeof(string)), ("NumberFlag", typeof(bool)), ("BooleanFlag", typeof(bool)));
        rows.Rows.Add("a-true", true, true);
        rows.Rows.Add("b-false", false, false);
        rows.Rows.Add("c-null", DBNull.Value, DBNull.Value);

        await Processor().SaveAsync(rows, "OracleTypes");

        (await _database.QueryAsync(SelectFlags, reader => (reader.GetString(0), NullableValue(reader, 1), NullableValue(reader, 2))))
            .Should().Equal(("a-true", "1", "True"), ("b-false", "0", "False"), ("c-null", null, null));
    }

    private Task SaveTestDbModelsAsync(IBulkProcessor processor, Shape shape, TestDbModel[] rows) => shape switch
    {
        Shape.Models => processor.SaveAsync(rows, TestDbModelsTable),
        Shape.DataTable => SaveDataTableAsync(processor, rows),
        Shape.DataReader => SaveReaderAsync(processor, rows),
        _ => SaveJsonAsync(processor, rows)
    };

    private static async Task SaveDataTableAsync(IBulkProcessor processor, TestDbModel[] rows)
    {
        using DataTable table = ContractRows.TestDbModelsDataTable(rows);
        await processor.SaveAsync(table, TestDbModelsTable);
    }

    private static async Task SaveReaderAsync(IBulkProcessor processor, TestDbModel[] rows)
    {
        using IDataReader reader = ContractRows.TestDbModelsReader(rows);
        await processor.SaveAsync(reader, TestDbModelsTable);
    }

    private static async Task SaveJsonAsync(IBulkProcessor processor, TestDbModel[] rows)
    {
        await using Stream json = ContractRows.TestDbModelsJson(TestDbModelsTable, rows);
        await processor.SaveAsync(json);
    }

    private static string NullableValue(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}
