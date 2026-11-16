using System;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Providers;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public class OracleIdentifierTests
{
    private static readonly string LongestValid = "A" + new string('b', 127);

    [TestCase("Posts")]
    [TestCase("POSTS")]
    [TestCase("blog_posts")]
    [TestCase("T1$#")]
    [TestCase("x")]
    public void RequireSimpleTableName_ShouldReturnTheName_WhenItIsASimpleIdentifier(string name)
    {
        OracleIdentifier.RequireSimpleTableName(name, "table").Should().Be(name);
        OracleIdentifier.RequireSimpleColumnName(name).Should().Be(name);
    }

    [Test]
    public void RequireSimpleTableName_ShouldAcceptTheName_WhenItIsExactly128Characters()
    {
        OracleIdentifier.RequireSimpleTableName(LongestValid, "table").Should().Be(LongestValid);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("Posts ")]
    [TestCase("1Posts")]
    [TestCase("_Posts")]
    [TestCase("Posts; DROP TABLE Blogs--")]
    [TestCase("TestDbModels; DROP TABLE Blogs--")]
    [TestCase("Posts--")]
    [TestCase("Po\"sts")]
    [TestCase("\"Posts\"")]
    [TestCase("[Posts]")]
    [TestCase("Posts]")]
    [TestCase("Posts'")]
    [TestCase("Po sts")]
    [TestCase("Posts\r\nX")]
    [TestCase("Posts\n")]
    [TestCase("Pösts")]
    [TestCase("Posts\0")]
    [TestCase("a.b")]
    public void RequireSimpleTableName_ShouldThrowAMarkedArgumentExceptionForTheCallersParameter_WhenTheNameIsNotASimpleIdentifier(string name)
    {
        Action action = () => OracleIdentifier.RequireSimpleTableName(name, "table");

        var thrown = action.Should().Throw<ArgumentException>()
            .WithMessage("The table '*' is not a valid unquoted Oracle identifier*OracleIdentifierResolution.CatalogLookup*");
        thrown.Which.ParamName.Should().Be("destinationTableName");
        OracleValidation.IsValidationFailure(thrown.Which).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("Posts ")]
    [TestCase("1Posts")]
    [TestCase("_Posts")]
    [TestCase("Posts; DROP TABLE Blogs--")]
    [TestCase("TestDbModels; DROP TABLE Blogs--")]
    [TestCase("Posts--")]
    [TestCase("Po\"sts")]
    [TestCase("\"Posts\"")]
    [TestCase("[Posts]")]
    [TestCase("Posts]")]
    [TestCase("Posts'")]
    [TestCase("Po sts")]
    [TestCase("Posts\r\nX")]
    [TestCase("Posts\n")]
    [TestCase("Pösts")]
    [TestCase("Posts\0")]
    [TestCase("a.b")]
    public void RequireSimpleColumnName_ShouldThrowAMarkedColumnMappingMismatch_WhenTheNameIsNotASimpleIdentifier(string name)
    {
        Action action = () => OracleIdentifier.RequireSimpleColumnName(name);

        var thrown = action.Should().Throw<InvalidOperationException>()
            .WithMessage($"{BulkCopyErrorMessages.ColumnMappingMismatch} (The destination column '*' is not a valid unquoted Oracle identifier*");
        OracleValidation.IsValidationFailure(thrown.Which).Should().BeTrue();
    }

    [Test]
    public void RequireSimpleTableName_ShouldThrow_WhenTheNameIsLongerThan128Characters()
    {
        Action action = () => OracleIdentifier.RequireSimpleTableName(LongestValid + "c", "table");

        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void RequireSimpleTableName_ShouldSanitiseTheEchoedName_WhenItContainsControlCharactersOrIsLong()
    {
        string hostile = "Posts\r\nFORGED LOG LINE" + new string('x', 300);

        Action action = () => OracleIdentifier.RequireSimpleTableName(hostile, "table");

        string message = action.Should().Throw<ArgumentException>().Which.Message;
        message.Should().NotContain("\r").And.NotContain("\n").And.Contain("Posts??FORGED").And.Contain("...");
        message.Should().NotContain(new string('x', 200));
    }

    [TestCase("Posts", "\"POSTS\"")]
    [TestCase("posts", "\"POSTS\"")]
    [TestCase("Date", "\"DATE\"")]
    [TestCase("Comment", "\"COMMENT\"")]
    public void QuoteFolded_ShouldUpperCaseAndQuote_WhenGivenASimpleIdentifier(string name, string expected)
    {
        OracleIdentifier.QuoteFolded(name).Should().Be(expected);
    }

    [TestCase("MixedCase", "\"MixedCase\"")]
    [TestCase("with space", "\"with space\"")]
    public void Quote_ShouldWrapTheExactName_WhenItIsAValidCatalogueName(string name, string expected)
    {
        OracleIdentifier.Quote(name).Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("A\"B")]
    [TestCase("A\0B")]
    public void Quote_ShouldThrowAMarkedInvalidOperationException_WhenTheCatalogueNameCannotBeQuoted(string name)
    {
        Action action = () => OracleIdentifier.Quote(name);

        OracleValidation.IsValidationFailure(action.Should().Throw<InvalidOperationException>().Which).Should().BeTrue();
    }

    [Test]
    public void Quote_ShouldThrow_WhenTheNameIsLongerThan128Characters()
    {
        Action action = () => OracleIdentifier.Quote(new string('a', 129));

        action.Should().Throw<InvalidOperationException>();
    }

    [TestCase("Posts", null, "Posts")]
    [TestCase("LOKI.Posts", "LOKI", "Posts")]
    [TestCase("\"Loki\".\"MixedCase\"", "\"Loki\"", "\"MixedCase\"")]
    public void Split_ShouldReturnSchemaAndTable_WhenGivenOneOrTwoParts(string qualified, string schema, string table)
    {
        OracleIdentifier.Split(qualified).Should().Be((schema, table));
    }

    [TestCase("a.b.c")]
    [TestCase("db..t")]
    public void Split_ShouldThrow_WhenGivenMoreThanTwoParts(string qualified)
    {
        Action action = () => OracleIdentifier.Split(qualified);

        action.Should().Throw<ArgumentException>().WithMessage("*must be 'TABLE' or 'SCHEMA.TABLE'*")
            .Which.ParamName.Should().Be("destinationTableName");
    }

    [TestCase("\"MixedCase\"", true, "MixedCase")]
    [TestCase("MixedCase", false, "MixedCase")]
    [TestCase("\"", false, "\"")]
    public void StripQuotes_ShouldRemoveOnePairOfQuotes_WhenTheNameIsQuoted(string name, bool quoted, string stripped)
    {
        OracleIdentifier.IsQuoted(name).Should().Be(quoted);
        OracleIdentifier.StripQuotes(name).Should().Be(stripped);
    }
}
