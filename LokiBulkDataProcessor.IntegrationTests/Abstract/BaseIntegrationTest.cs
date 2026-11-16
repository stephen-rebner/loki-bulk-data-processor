using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;
using LokiBulkDataProcessor.IntegrationTests.Contract.Mappings;
using LokiBulkDataProcessor.IntegrationTests.EF;
using LokiBulkDataProcessor.IntegrationTests.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Abstract
{
    /// <summary>
    /// Base for the SQL Server-only fixtures (the provider-neutral tests live in <see cref="SqlServerContractTests"/>).
    /// One container per fixture; the database is re-created before each test. <see cref="BulkProcessor"/> comes from the
    /// legacy <c>AddLokiBulkDataProcessor(connectionString, mappingAssembly)</c> registration, resolved in a new scope per test.
    /// </summary>
    public abstract class BaseIntegrationTest
    {
        private SqlServerTestDatabase _database;
        private ServiceProvider _serviceProvider;
        private IServiceScope _scope;

        protected TestDbContext TestDbContext;

        protected IBulkProcessor BulkProcessor;

        [OneTimeSetUp]
        public async Task StartDatabaseAsync()
        {
            _database = new SqlServerTestDatabase();
            await _database.StartAsync();
        }

        [OneTimeTearDown]
        public async Task StopDatabaseAsync()
        {
            if (_database is not null)
            {
                await _database.DisposeAsync();
            }
        }

        [SetUp]
        public async Task TestSetup()
        {
            await _database.ResetAsync();
            TestDbContext = _database.CreateDbContext();
            _serviceProvider = BuildLegacyServices();
            _scope = _serviceProvider.CreateScope();
            BulkProcessor = _scope.ServiceProvider.GetRequiredService<IBulkProcessor>();
        }

        [TearDown]
        public async Task TestCleanup()
        {
            _scope?.Dispose();
            await (_serviceProvider?.DisposeAsync() ?? ValueTask.CompletedTask);
            await (TestDbContext?.DisposeAsync() ?? ValueTask.CompletedTask);
        }

        protected string GetConnectionString() => _database.ConnectionString;

        protected void SaveEntities<T>(params T[] entities) where T : class
        {
            TestDbContext.AttachRange(entities);
            TestDbContext.SaveChanges();
        }

        protected async Task TheDatabaseTableShouldBeEmpty<T>() where T : class
        {
            var numberOfRecords = await TestDbContext.Set<T>().CountAsync();

            numberOfRecords.Should().Be(0);
        }

        private ServiceProvider BuildLegacyServices()
        {
            var services = new ServiceCollection();
            services.AddLokiBulkDataProcessor(GetConnectionString(), typeof(PostMapping).Assembly);
            return services.BuildServiceProvider();
        }
    }
}
