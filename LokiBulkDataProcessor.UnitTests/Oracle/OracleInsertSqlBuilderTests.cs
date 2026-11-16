using System;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Oracle.Internal.Writers;
using Loki.BulkDataProcessor.Providers;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public class OracleInsertSqlBuilderTests
{
    [Test]
    public void Build_ShouldReturnAPositionallyBoundInsert_WhenTheTargetHasNoSchema()
    {
        var target = new OracleResolvedTarget(null, "\"POSTS\"",
            new[] { new OracleResolvedColumn("Title", "\"TITLE\""), new OracleResolvedColumn("BlogId", "\"BLOGID\"") });

        OracleInsertSqlBuilder.Build(target).Should().Be("INSERT INTO \"POSTS\" (\"TITLE\", \"BLOGID\") VALUES (:1, :2)");
    }

    [Test]
    public void Build_ShouldQualifyTheTable_WhenTheTargetHasASchema()
    {
        var target = new OracleResolvedTarget("\"LOKI\"", "\"MixedCase\"", new[] { new OracleResolvedColumn("Id", "\"Id\"") });

        OracleInsertSqlBuilder.Build(target).Should().Be("INSERT INTO \"LOKI\".\"MixedCase\" (\"Id\") VALUES (:1)");
    }

    [Test]
    public void Build_ShouldNeverContainSourceNames_WhenSourceColumnsAreHostile()
    {
        var resolved = UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("Posts",
            new[] { new ColumnMap("x'); DROP TABLE Blogs--", "Title") }));

        string sql = OracleInsertSqlBuilder.Build(resolved);

        sql.Should().Be("INSERT INTO \"POSTS\" (\"TITLE\") VALUES (:1)");
    }

    [Test]
    public void Build_ShouldNumberEveryParameter_WhenThereAreManyColumns()
    {
        var columns = new OracleResolvedColumn[12];
        for (int i = 0; i < columns.Length; i++)
        {
            columns[i] = new OracleResolvedColumn($"C{i}", $"\"C{i}\"");
        }

        string sql = OracleInsertSqlBuilder.Build(new OracleResolvedTarget(null, "\"T\"", columns));

        sql.Should().EndWith("VALUES (:1, :2, :3, :4, :5, :6, :7, :8, :9, :10, :11, :12)");
        sql.Should().NotContain(Environment.NewLine);
    }
}
