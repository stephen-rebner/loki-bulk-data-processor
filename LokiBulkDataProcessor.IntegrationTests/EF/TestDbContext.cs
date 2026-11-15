using Microsoft.EntityFrameworkCore;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;

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

        /// <summary>The name of TestDbModels' identity key, mapped as a shadow property.</summary>
        public const string TestDbModelKey = "Id";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // TestDbModel deliberately has no Id (the contract never writes identity columns); the table keeps its
            // identity Id as a shadow key, so the schema and the migrations are unchanged.
            modelBuilder.Entity<TestDbModel>().Property<int>(TestDbModelKey);
            modelBuilder.Entity<TestDbModel>().HasKey(TestDbModelKey);
        }
    }
}
