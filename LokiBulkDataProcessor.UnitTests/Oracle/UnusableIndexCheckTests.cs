using System;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Oracle.Internal.Writers;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

/// <summary>
/// <see cref="UnusableIndexCheck"/> without a database: the constant catalogue query, the probe SQL built from catalogue names and the
/// fixed message. The check itself (ORA-01502 inside the loading transaction) is covered by <c>OracleDirectPathTests</c>.
/// </summary>
public class UnusableIndexCheckTests
{
    private static readonly OracleResolvedTarget Qualified = new("\"LOKI\"", "\"KEYEDROWS\"", Array.Empty<OracleResolvedColumn>());

    [Test]
    public void UniqueIndexQuery_ShouldUseBindVariablesForTheOwnerAndTable_WhenItIsBuilt()
    {
        UnusableIndexCheck.UniqueIndexQuery.Should().Contain("NVL(:owner, SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA'))")
            .And.Contain("i.TABLE_NAME = :tableName").And.Contain("i.UNIQUENESS = 'UNIQUE'");
    }

    [Test]
    public void ProbeSql_ShouldTouchTheQuotedIndexWithOneRow_WhenTheIndexIsNotPartitioned()
    {
        string sql = UnusableIndexCheck.ProbeSql(Qualified, new UnusableIndexCheck.UniqueIndex("PK_KeyedRows", "ID", Partitioned: false));

        sql.Should().Be("SELECT /*+ INDEX_FFS(t \"PK_KeyedRows\") */ COUNT(*) FROM \"LOKI\".\"KEYEDROWS\" t WHERE \"ID\" IS NOT NULL AND ROWNUM = 1");
    }

    [Test]
    public void ProbeSql_ShouldScanEveryPartition_WhenTheIndexIsPartitioned()
    {
        var unqualified = new OracleResolvedTarget(null, "\"KEYEDROWS\"", Array.Empty<OracleResolvedColumn>());

        string sql = UnusableIndexCheck.ProbeSql(unqualified, new UnusableIndexCheck.UniqueIndex("UX_KEYS", "NAME", Partitioned: true));

        sql.Should().Be("SELECT /*+ INDEX_FFS(t \"UX_KEYS\") */ COUNT(*) FROM \"KEYEDROWS\" t WHERE \"NAME\" IS NOT NULL");
    }

    [TestCase("PK\"; DROP TABLE Blogs--", "ID")]
    [TestCase("PK_KEYEDROWS", "ID\") */ 1 FROM DUAL --")]
    public void ProbeSql_ShouldThrow_WhenACatalogueNameContainsADoubleQuote(string indexName, string column)
    {
        Action action = () => UnusableIndexCheck.ProbeSql(Qualified, new UnusableIndexCheck.UniqueIndex(indexName, column, false));

        action.Should().Throw<InvalidOperationException>().Which.Should().Match<Exception>(e => OracleValidation.IsValidationFailure(e));
    }

    [Test]
    public void OwnerOf_ShouldBeTheUnquotedSchemaOrNull_WhenTheTargetIsResolved()
    {
        UnusableIndexCheck.OwnerOf(Qualified).Should().Be("LOKI");
        UnusableIndexCheck.OwnerOf(new OracleResolvedTarget(null, "\"T\"", Array.Empty<OracleResolvedColumn>())).Should().BeNull();
    }

    [Test]
    public void Message_ShouldNameTheSanitisedTableAndAskForARollback_WhenBuilt()
    {
        string message = UnusableIndexCheck.Message("Keyed\r\nRows");

        message.Should().StartWith("The direct-path load into 'Keyed??Rows' left a unique index of the table UNUSABLE")
            .And.Contain("roll that transaction back").And.Contain("Rolling back restores the index.");
    }
}
