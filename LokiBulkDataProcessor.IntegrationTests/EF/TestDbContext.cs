using Microsoft.EntityFrameworkCore;
using LokiBulkDataProcessor.IntegrationTests.TestModels;

namespace LokiBulkDataProcessor.IntegrationTests.EF
{
    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
        {
        }

        public DbSet<TestDbModel> TestDbModels { get; set; }

        public DbSet<Post> Posts { get; set; }

        public DbSet<Blog> Blogs { get; set; }
    }
}
