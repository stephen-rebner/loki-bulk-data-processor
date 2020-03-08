using Microsoft.EntityFrameworkCore;
using LokiBulkDataProcessor.IntegrationTests.TestModels;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace LokiBulkDataProcessor.IntegrationTests.EF
{
    public class TestDbContext : DbContext, IDesignTimeDbContextFactory<TestDbContext>
    {
        public TestDbContext() { }

        public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
        {
        }

        public DbSet<TestDbModel> TestDbModels { get; set; }

        public DbSet<Post> Posts { get; set; }

        public DbSet<Blog> Blogs { get; set; }

        public TestDbContext CreateDbContext(string[] args)
        {
            var serviceProvider = new ServiceCollection()
            .AddEntityFrameworkSqlServer()
            .BuildServiceProvider();

            var builder = new DbContextOptionsBuilder<TestDbContext>();

            builder.UseSqlServer(
                "Server=(local);Database=IntegrationTestsDb;Trusted_Connection=True;MultipleActiveResultSets=true")
                    .UseInternalServiceProvider(serviceProvider);

            return new TestDbContext(builder.Options);
        }
    }
}
