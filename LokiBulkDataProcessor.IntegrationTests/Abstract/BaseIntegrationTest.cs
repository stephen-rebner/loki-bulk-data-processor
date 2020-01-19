using System.Transactions;
using LokiBulkDataProcessor.IntegrationTests.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Abstract
{
    public abstract class BaseIntegrationTest
    {
        protected TestDbContext TestDbContext;

        protected TransactionScope TransactionScope;

        [SetUp]
        public void TestSetup()
        {
            TestDbContext = new TestDbContext().CreateDbContext(new string[0]);

            TestDbContext.Database.EnsureDeleted();
            TestDbContext.Database.Migrate();
        }

        [TearDown]
        public void TestCleanup()
        {
            TestDbContext.Dispose();
        }
    }
}
