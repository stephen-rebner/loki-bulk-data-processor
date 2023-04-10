
using Loki.BulkDataProcessor;
using LokiBulkDataProcessor.IntegrationTests.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using FluentAssertions;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestContainers.Container.Abstractions.Hosting;
//using Container.Database.MsSql.Integration.Tests.Fixtures;
using TestContainers.Container.Database.Hosting;
using TestContainers.Container.Database.MsSql;
namespace LokiBulkDataProcessor.IntegrationTests.Abstract
{
    public abstract class BaseIntegrationTest
    {
        private const string TestDbName = "IntegrationTestsDb";

        private MsSqlContainer MsSqlContainer;
        
        protected TestDbContext TestDbContext;
        
        protected IBulkProcessor BulkProcessor;

        protected string GetConnectionString()
        {
            return MsSqlContainer.GetConnectionString(TestDbName);
        }
        
        [SetUp]
        protected async Task TestSetup() 
        {
            MsSqlContainer = InitContainerTest();

            await MsSqlContainer.StartAsync();

            var serviceCollection = ConfigureServiceCollection();
            
            ConfigureStartUp(serviceCollection);
            
            var serviceProvider = serviceCollection.BuildServiceProvider();

            await MsSqlContainer.StartAsync();

            var dbContextOptionsBuilder = BuildDbContextOptionsBuilder(serviceProvider);

            TestDbContext = new TestDbContext(dbContextOptionsBuilder.Options);

            CreateDatabase();
            
            BulkProcessor = serviceProvider.GetService<IBulkProcessor>();
        }

        private DbContextOptionsBuilder<TestDbContext> BuildDbContextOptionsBuilder(ServiceProvider serviceProvider)
        {
            var dbContextOptionsBuilder = new DbContextOptionsBuilder<TestDbContext>();

            dbContextOptionsBuilder.UseSqlServer(GetConnectionString())
                .UseInternalServiceProvider(serviceProvider);

            return dbContextOptionsBuilder;
        }

        [TearDown]
        protected async Task TestCleanup()
        {
            await TestDbContext.DisposeAsync();
            await MsSqlContainer.StopAsync();
        }

        private MsSqlContainer InitContainerTest()
        {
            const string username = "sa";
            const string password = "Pwd12345678!";
            const string database = "IntegrationTestsDb";
            MsSqlContainer = new ContainerBuilder<MsSqlContainer>()
                .ConfigureDatabaseConfiguration(username, password, database)
                .Build();

            return MsSqlContainer;
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

        protected async Task ShouldExistInTheDatabase<T>(IEnumerable<T> expectedDomainObjects) where T : class
        {
            var actualDomainObjects = await LoadAllEntities<T>();

            expectedDomainObjects.Should().BeEquivalentTo(actualDomainObjects);
        }

        protected async Task TheDatabaseTableShouldBeEmpty<T>() where T : class
        {
            var numberOfRecords = await TestDbContext.Set<T>().CountAsync();

            numberOfRecords.Should().Be(0);
        }

        private void CreateDatabase()
        {
            TestDbContext.Database.EnsureDeleted();
            TestDbContext.Database.Migrate();
        }

        private IServiceCollection ConfigureServiceCollection()
        {
            IServiceCollection services = new ServiceCollection();
            
            services.AddEntityFrameworkSqlServer();

            return services;
            
            
            
        }

        private void ConfigureStartUp(IServiceCollection serviceCollection)
        {
            var startup = new Startup(GetConnectionString());
            
            startup.ConfigureServices(serviceCollection);
        }
    }
}
