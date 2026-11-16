using System;
using System.Threading.Tasks;
using System.Data;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Internal;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Oracle.Internal.Identifiers;
using Loki.BulkDataProcessor.Oracle.Internal.Writers;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public class OracleBulkCopyProviderTests
{
    private const string ConnectionString = "User Id=loki;Password=fake;Data Source=fake:1521/FREEPDB1";

    private readonly OracleBulkCopyProvider _provider = new(OracleProvider.Name, new OracleProviderOptions().Validated());

    [Test]
    public void Name_ShouldReturnTheRegisteredName_WhenConstructedWithAName()
    {
        _provider.Name.Should().Be("Oracle");
        new OracleBulkCopyProvider("Custom", new OracleProviderOptions()).Name.Should().Be("Custom");
    }

    [Test]
    public void CreateConnection_ShouldReturnAnUnopenedOracleConnection_WhenGivenAConnectionString()
    {
        using var connection = _provider.CreateConnection(ConnectionString);

        connection.Should().BeOfType<OracleConnection>();
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Test]
    public void AdoptExternalTransaction_ShouldThrowArgumentNullException_WhenTheTransactionIsNull()
    {
        Action action = () => _provider.AdoptExternalTransaction(null);

        action.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("transaction");
    }

    [Test]
    public void AdoptExternalTransaction_ShouldThrowArgumentException_WhenTheTransactionIsNotAnOracleTransaction()
    {
        var foreignTransaction = new Mock<IDbTransaction>().Object;

        Action action = () => _provider.AdoptExternalTransaction(foreignTransaction);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*requires an Oracle.ManagedDataAccess.Client.OracleTransaction*")
            .WithMessage($"*{foreignTransaction.GetType().FullName}*")
            .And.ParamName.Should().Be("transaction");
    }

    [Test]
    public void AdoptExternalTransaction_ShouldThrowArgumentException_WhenTheTransactionIsASqlServerTransaction()
    {
        var sqlTransaction = (SqlTransaction)RuntimeHelpers.GetUninitializedObject(typeof(SqlTransaction));

        Action action = () => _provider.AdoptExternalTransaction(sqlTransaction);

        action.Should().Throw<ArgumentException>().WithMessage("*Microsoft.Data.SqlClient.SqlTransaction*")
            .Which.ParamName.Should().Be("transaction");
    }

    [Test]
    public void AdoptExternalTransaction_ShouldReturnTheSameTransaction_WhenItIsAnOracleTransaction()
    {
        var oracleTransaction = (OracleTransaction)RuntimeHelpers.GetUninitializedObject(typeof(OracleTransaction));

        _provider.AdoptExternalTransaction(oracleTransaction).Should().BeSameAs(oracleTransaction);
    }

    [Test]
    public void Transaction_ShouldThrowArgumentException_WhenSetToANonOracleTransaction()
    {
        IBulkProcessor processor = new BulkProcessor(_provider, ConnectionString, MappingRegistry.Empty, NullLoggerFactory.Instance);

        Action action = () => processor.Transaction = new Mock<IDbTransaction>().Object;

        action.Should().Throw<ArgumentException>().WithMessage("*requires an Oracle.ManagedDataAccess.Client.OracleTransaction*");
        processor.Transaction.Should().BeNull();
    }

    [Test]
    public async Task CreateWriter_ShouldReturnAnArrayBindWriter_WhenTheWriteModeIsArrayBind()
    {
        using var connection = new OracleConnection(ConnectionString);
        var context = new BulkWriteContext(connection, null, new BulkWriteOptions(10, 20), NullLoggerFactory.Instance);

        IBulkWriter writer = _provider.CreateWriter(context);

        writer.Should().BeOfType<ArrayBindOracleWriter>();
        await writer.Invoking(w => w.DisposeAsync().AsTask()).Should().NotThrowAsync();
    }

    [Test]
    public void CreateWriter_ShouldThrowArgumentNullException_WhenTheContextIsNull()
    {
        Action action = () => _provider.CreateWriter(null);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void CreateWriter_ShouldWork_WhenTheContextHasNoLoggerFactory()
    {
        using var connection = new OracleConnection(ConnectionString);

        Action action = () => _provider.CreateWriter(new BulkWriteContext(connection, null, new BulkWriteOptions(0, 0), null));

        action.Should().NotThrow();
    }

    [TestCase(OracleIdentifierResolution.Unquoted, typeof(UnquotedIdentifierResolver))]
    [TestCase(OracleIdentifierResolution.CatalogLookup, typeof(CatalogIdentifierResolver))]
    public void Constructor_ShouldPickTheResolver_WhenGivenAnIdentifierResolution(OracleIdentifierResolution resolution, Type expected)
    {
        var provider = new OracleBulkCopyProvider("Oracle", new OracleProviderOptions { IdentifierResolution = resolution }.Validated());

        provider.Resolver.Should().BeOfType(expected);
    }

    [TestCase(OracleBooleanBinding.Number)]
    [TestCase(OracleBooleanBinding.Boolean)]
    public void Constructor_ShouldUseTheBooleanBinding_WhenGivenOptions(OracleBooleanBinding binding)
    {
        var provider = new OracleBulkCopyProvider("Oracle", new OracleProviderOptions { BooleanBinding = binding }.Validated());

        provider.TypeMap.BooleanBinding.Should().Be(binding);
    }

    [Test]
    public void TranslateException_ShouldReturnANewExceptionWrappingTheOriginal_WhenTheWriteFails()
    {
        var original = new InvalidOperationException("boom");

        Exception translated = _provider.TranslateException(original, new BulkWriteTarget("T", Array.Empty<ColumnMap>()));

        translated.Should().NotBeSameAs(original);
        translated.InnerException.Should().BeSameAs(original);
    }
}
