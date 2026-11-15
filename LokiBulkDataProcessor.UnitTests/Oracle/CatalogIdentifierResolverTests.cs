using System;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Providers;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;
using CatalogRow = Loki.BulkDataProcessor.Oracle.Internal.Identifiers.CatalogIdentifierResolver.CatalogRow;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public class CatalogIdentifierResolverTests
{
    private static readonly CatalogRow[] MixedCaseRows =
    {
        new("LOKI", "MixedCase", "Id"),
        new("LOKI", "MixedCase", "Name"),
        new("LOKI", "MixedCase", "NAME_UPPER"),
    };

    [Test]
    public void Resolve_ShouldQuoteTheCatalogueNames_WhenTheRequestMatchesCaseInsensitively()
    {
        var target = new BulkWriteTarget("mixedcase", new[] { new ColumnMap("Id", "ID"), new ColumnMap("Name", "name") });

        OracleResolvedTarget resolved = CatalogIdentifierResolver.Resolve(MixedCaseRows, target);

        resolved.QualifiedTableSql.Should().Be("\"LOKI\".\"MixedCase\"");
        resolved.Columns.Should().Equal(new OracleResolvedColumn("Id", "\"Id\""), new OracleResolvedColumn("Name", "\"Name\""));
    }

    [Test]
    public void Resolve_ShouldPreferTheExactMatch_WhenTwoObjectsDifferOnlyInCase()
    {
        CatalogRow[] rows = { new("LOKI", "Posts", "Title"), new("LOKI", "POSTS", "TITLE") };

        OracleResolvedTarget resolved = CatalogIdentifierResolver.Resolve(rows, new BulkWriteTarget("Posts", new[] { new ColumnMap("Title", "Title") }));

        resolved.TableSql.Should().Be("\"Posts\"");
        resolved.Columns[0].DestinationSql.Should().Be("\"Title\"");
    }

    [Test]
    public void Resolve_ShouldThrow_WhenTheNameMatchesMoreThanOneObjectCaseInsensitively()
    {
        CatalogRow[] rows = { new("LOKI", "Posts", "Title"), new("LOKI", "POSTS", "TITLE") };

        Action action = () => CatalogIdentifierResolver.Resolve(rows, new BulkWriteTarget("posts", new[] { new ColumnMap("Title", "Title") }));

        action.Should().Throw<InvalidOperationException>().WithMessage("*matches more than one object case-insensitively; quote it*");
    }

    [Test]
    public void Resolve_ShouldMatchExactlyOnly_WhenTheRequestedNameIsQuoted()
    {
        Action action = () => CatalogIdentifierResolver.Resolve(MixedCaseRows, new BulkWriteTarget("\"MIXEDCASE\"", new[] { new ColumnMap("Id", "Id") }));

        action.Should().Throw<InvalidOperationException>().WithMessage("The destination table '\"MIXEDCASE\"' was not found*");
    }

    [Test]
    public void Resolve_ShouldEchoTheCallersTableName_WhenTheCatalogueReturnsNothing()
    {
        Action action = () => CatalogIdentifierResolver.Resolve(Array.Empty<CatalogRow>(), new BulkWriteTarget("NonExistentTable", new[] { new ColumnMap("A", "A") }));

        var thrown = action.Should().Throw<InvalidOperationException>()
            .WithMessage("The destination table 'NonExistentTable' was not found or is not visible to the connected user.");
        OracleValidation.IsValidationFailure(thrown.Which).Should().BeTrue();
    }

    [Test]
    public void Resolve_ShouldThrowTheColumnMappingMismatchMessage_WhenADestinationColumnIsMissing()
    {
        Action action = () => CatalogIdentifierResolver.Resolve(MixedCaseRows, new BulkWriteTarget("MixedCase", new[] { new ColumnMap("X", "Nope\r\n") }));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"{BulkCopyErrorMessages.ColumnMappingMismatch} ('Nope??' not found)");
    }

    [Test]
    public void Resolve_ShouldThrow_WhenTheCatalogueReturnsANameContainingAQuote()
    {
        CatalogRow[] rows = { new("LOKI", "Bad\"Name", "A") };

        Action action = () => CatalogIdentifierResolver.Resolve(rows, new BulkWriteTarget("Bad\"Name", new[] { new ColumnMap("A", "A") }));

        action.Should().Throw<ArgumentException>().WithMessage("Invalid Oracle identifier returned from the catalogue.*");
    }

    [Test]
    public void Resolve_ShouldPickTheRequestedSchema_WhenTheTableExistsInSeveralSchemas()
    {
        CatalogRow[] rows = { new("LOKI", "POSTS", "TITLE"), new("OTHER", "POSTS", "TITLE") };

        OracleResolvedTarget resolved = CatalogIdentifierResolver.Resolve(rows, new BulkWriteTarget("other.Posts", new[] { new ColumnMap("Title", "Title") }));

        resolved.QualifiedTableSql.Should().Be("\"OTHER\".\"POSTS\"");
    }

    [Test]
    public void CreateCatalogCommand_ShouldBindTheCallersNames_WhenBuildingTheQuery()
    {
        using var connection = new OracleConnection();
        string hostile = "Posts' OR '1'='1";

        using OracleCommand command = CatalogIdentifierResolver.CreateCatalogCommand(connection, null, hostile);

        command.CommandText.Should().Be(CatalogIdentifierResolver.CatalogQuery).And.NotContain(hostile);
        command.BindByName.Should().BeTrue();
        command.Parameters["owner"].Value.Should().Be(DBNull.Value);
        command.Parameters["tableName"].Value.Should().Be(hostile);
    }
}
