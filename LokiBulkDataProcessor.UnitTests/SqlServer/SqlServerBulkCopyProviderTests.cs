using System;
using System.Data;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.Internal;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Loki.BulkDataProcessor.Providers;
using Loki.BulkDataProcessor.SqlServer;
using Loki.BulkDataProcessor.SqlServer.Internal;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.SqlServer;

public class SqlServerBulkCopyProviderTests
{
    private const string ConnectionString = "Server=fake;Database=fake";

    private readonly SqlServerBulkCopyProvider _provider = new(SqlServerProvider.Name);

    [Test]
    public void Name_ShouldReturnTheRegisteredName_WhenConstructedWithAName()
    {
        _provider.Name.Should().Be("SqlServer");
        new SqlServerBulkCopyProvider("Custom").Name.Should().Be("Custom");
    }

    [Test]
    public void CreateConnection_ShouldReturnAnUnopenedSqlConnection_WhenGivenAConnectionString()
    {
        using var connection = _provider.CreateConnection(ConnectionString);

        connection.Should().BeOfType<SqlConnection>();
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Test]
    public void AdoptExternalTransaction_ShouldThrowArgumentException_WhenTheTransactionIsNotAMicrosoftDataSqlClientTransaction()
    {
        var foreignTransaction = new Mock<IDbTransaction>().Object;

        Action action = () => _provider.AdoptExternalTransaction(foreignTransaction);

        action.Should()
            .Throw<ArgumentException>()
            .WithMessage("*requires a Microsoft.Data.SqlClient.SqlTransaction*")
            .WithMessage($"*{foreignTransaction.GetType().FullName}*")
            .And.ParamName.Should().Be("transaction");
    }

    [Test]
    public void Transaction_ShouldThrowArgumentException_WhenSetToANonMicrosoftDataSqlClientTransaction()
    {
        IBulkProcessor processor = new BulkProcessor(_provider, ConnectionString, MappingRegistry.Empty, NullLoggerFactory.Instance);

        Action action = () => processor.Transaction = new Mock<IDbTransaction>().Object;

        action.Should().Throw<ArgumentException>().WithMessage("*requires a Microsoft.Data.SqlClient.SqlTransaction*");
        processor.Transaction.Should().BeNull();
    }

    [Test]
    public void AdoptExternalTransaction_ShouldThrowArgumentNullException_WhenTheTransactionIsNull()
    {
        Action action = () => _provider.AdoptExternalTransaction(null);

        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void CreateWriter_ShouldConfigureSqlBulkCopyFromTheContextAndTarget_WhenTheTargetIsConfigured()
    {
        using var connection = new SqlConnection(ConnectionString);
        var context = new BulkWriteContext(connection, null, new BulkWriteOptions(123, 45), NullLoggerFactory.Instance);
        var writer = (SqlServerBulkWriter)_provider.CreateWriter(context);

        writer.ConfigureTarget(new BulkWriteTarget("dbo.Posts", new[] { new ColumnMap("ATitle", "Title"), new ColumnMap("B", "B") }));

        writer.BulkCopy.BatchSize.Should().Be(123);
        writer.BulkCopy.BulkCopyTimeout.Should().Be(45);
        writer.BulkCopy.DestinationTableName.Should().Be("dbo.Posts");
        writer.BulkCopy.ColumnMappings.Count.Should().Be(2);
        writer.BulkCopy.ColumnMappings[0].SourceColumn.Should().Be("ATitle");
        writer.BulkCopy.ColumnMappings[0].DestinationColumn.Should().Be("Title");
        SqlServerBulkWriter.CopyOptions.Should().Be(SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepNulls);
        writer.Invoking(w => w.DisposeAsync().AsTask().GetAwaiter().GetResult()).Should().NotThrow();
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
