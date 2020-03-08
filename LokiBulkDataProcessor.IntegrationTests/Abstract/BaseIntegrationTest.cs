using System.Collections.Generic;
using System.Threading.Tasks;
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

        [SetUp]
        protected void TestSetup()
        {
            TestDbContext = new TestDbContext().CreateDbContext(null);

            TestDbContext.Database.EnsureDeleted();
            TestDbContext.Database.Migrate();
        }

        [TearDown]
        protected void TestCleanup()
        {
            TestDbContext.Dispose();
        }

        protected async Task<IEnumerable<T>> LoadAllEntities<T>() where T : class
        {
            return await TestDbContext.Set<T>().ToListAsync();
        }

        protected void SaveEntities<T>(params T[] entities) where T : class
        {
            TestDbContext.AttachRange(entities);
            TestDbContext.SaveChanges();
        }
    }
}
