using Loki.BulkDataProcessor;
using LokiBulkDataProcessor.IntegrationTests.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LokiBulkDataProcessor.IntegrationTests.Abstract
{
    public abstract class BaseIntegrationTest
    {
        protected TestDbContext TestDbContext;
        protected IBulkProcessor BulkProcessor;

        [SetUp]
        protected void TestSetup()
        {
            TestDbContext = new TestDbContext().CreateDbContext(null);

            TestDbContext.Database.EnsureDeleted();
            TestDbContext.Database.Migrate();

            ResolveStartup();
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

        private void ResolveStartup()
        {
            IServiceCollection services = new ServiceCollection();
            var startup = new Startup();
            startup.ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            BulkProcessor = serviceProvider.GetService<IBulkProcessor>();
            //IServiceProvider serviceProvider = services.BuildServiceProvider();
        }
    }
}
