using System;
using System.Data;
using FluentAssertions;
using Loki.BulkDataProcessor.Context.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations;
using Moq;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.InternalDbOperations
{
    public class LokiDbConnectionTests
    {
        private Mock<IAppContext> _appContext;

        [SetUp]
        public void SetUp()
        {
            _appContext = new Mock<IAppContext>();
        }

        [Test]
        public void Create_ShouldThrowArgumentException_WhenExternalTransactionIsNotAMicrosoftDataSqlClientTransaction()
        {
            var foreignTransaction = new Mock<IDbTransaction>().Object;
            GivenTheExternalTransaction(foreignTransaction);

            Action action = () => LokiDbConnection.Create(_appContext.Object);

            action.Should()
                .Throw<ArgumentException>()
                .WithMessage("*requires a Microsoft.Data.SqlClient.SqlTransaction*")
                .WithMessage($"*{foreignTransaction.GetType().FullName}*")
                .And.ParamName.Should().Be("transaction");
        }

        private void GivenTheExternalTransaction(IDbTransaction transaction)
        {
            _appContext.SetupGet(x => x.ExternalTransaction).Returns(transaction);
            _appContext.SetupGet(x => x.IsUsingExternalTransaction).Returns(true);
        }
    }
}
