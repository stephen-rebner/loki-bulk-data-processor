using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Providers;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public class UnquotedIdentifierResolverTests
{
    [Test]
    public async Task ResolveAsync_ShouldFoldAndQuoteEveryName_WhenTheTargetIsSchemaQualified()
    {
        var target = new BulkWriteTarget("loki.Posts", new[] { new ColumnMap("Title", "Title"), new ColumnMap("Body", "Content") });

        OracleResolvedTarget resolved = await new UnquotedIdentifierResolver().ResolveAsync(null, target, 30, CancellationToken.None);

        resolved.SchemaSql.Should().Be("\"LOKI\"");
        resolved.TableSql.Should().Be("\"POSTS\"");
        resolved.QualifiedTableSql.Should().Be("\"LOKI\".\"POSTS\"");
        resolved.Columns.Should().Equal(new OracleResolvedColumn("Title", "\"TITLE\""), new OracleResolvedColumn("Body", "\"CONTENT\""));
    }

    [Test]
    public void Resolve_ShouldHaveNoSchema_WhenTheTableIsNotQualified()
    {
        OracleResolvedTarget resolved = UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("Posts", Array.Empty<ColumnMap>()));

        resolved.SchemaSql.Should().BeNull();
        resolved.QualifiedTableSql.Should().Be("\"POSTS\"");
    }

    [TestCase("TestDbModels; DROP TABLE Blogs--")]
    [TestCase("Posts]")]
    [TestCase("\"MixedCase\"")]
    [TestCase("Bad Schema.Posts")]
    [TestCase("a.b.c")]
    [TestCase("")]
    public void Resolve_ShouldThrowAMarkedArgumentExceptionForTheCallersParameter_WhenTheTableNameIsHostileOrInvalid(string tableName)
    {
        Action action = () => UnquotedIdentifierResolver.Resolve(new BulkWriteTarget(tableName, new[] { new ColumnMap("A", "A") }));

        var thrown = action.Should().Throw<ArgumentException>();
        thrown.Which.ParamName.Should().Be("destinationTableName");
        OracleValidation.IsValidationFailure(thrown.Which).Should().BeTrue();
    }

    [TestCase("Title\"; DROP TABLE Blogs--")]
    [TestCase("Ti]tle")]
    [TestCase("Ti;tle")]
    [TestCase("Ti--tle")]
    [TestCase("\"Title\"")]
    public void Resolve_ShouldThrowAMarkedColumnMappingMismatch_WhenADestinationColumnIsHostile(string column)
    {
        Action action = () => UnquotedIdentifierResolver.Resolve(new BulkWriteTarget("Posts", new[] { new ColumnMap("Title", column) }));

        var thrown = action.Should().Throw<InvalidOperationException>()
            .WithMessage($"{BulkCopyErrorMessages.ColumnMappingMismatch} (The destination column '*");
        OracleValidation.IsValidationFailure(thrown.Which).Should().BeTrue();
    }
}
