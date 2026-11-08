using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.TestModels;
using LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests
{
    /// <summary>
    /// Issue #14: Loki must accept the Microsoft.Data.SqlClient transaction behind an EF Core
    /// <see cref="IDbContextTransaction"/> and its writes must commit and roll back with it.
    /// </summary>
    public class BulkProcessorEfCoreTransactionTests : BaseIntegrationTest
    {
        [Test]
        public async Task SaveAsync_ShouldCommitWithTheEfCoreTransaction_WhenTheEfCoreTransactionIsCommitted()
        {
            await using var efTransaction = await TestDbContext.Database.BeginTransactionAsync();
            var blog = GivenABlogSavedByEfCoreInsideTheTransaction();
            GivenTheEfCoreTransactionIsPassedToTheBulkProcessor(efTransaction);

            await BulkProcessor.SaveAsync(ThesePostsForTheBlog(blog), nameof(TestDbContext.Posts));
            await efTransaction.CommitAsync();

            (await TestDbContext.Blogs.CountAsync()).Should().Be(1);
            (await TestDbContext.Posts.CountAsync()).Should().Be(2);
        }

        [Test]
        public async Task SaveAsync_ShouldRollBackWithTheEfCoreTransaction_WhenTheEfCoreTransactionIsRolledBack()
        {
            await using var efTransaction = await TestDbContext.Database.BeginTransactionAsync();
            var blog = GivenABlogSavedByEfCoreInsideTheTransaction();
            GivenTheEfCoreTransactionIsPassedToTheBulkProcessor(efTransaction);

            await BulkProcessor.SaveAsync(ThesePostsForTheBlog(blog), nameof(TestDbContext.Posts));
            await efTransaction.RollbackAsync();

            await TheDatabaseTableShouldBeEmpty<Post>();
            await TheDatabaseTableShouldBeEmpty<Blog>();
        }

        private Blog GivenABlogSavedByEfCoreInsideTheTransaction()
        {
            var blog = TestObjectFactory.NewBlog()
                .WithUrl("http://a-url.com")
                .Build();

            SaveEntities(blog);

            return blog;
        }

        private void GivenTheEfCoreTransactionIsPassedToTheBulkProcessor(IDbContextTransaction efTransaction)
        {
            var dbTransaction = efTransaction.GetDbTransaction();

            dbTransaction.Should().BeOfType<SqlTransaction>();

            BulkProcessor.Transaction = dbTransaction;
        }

        private static IEnumerable<Post> ThesePostsForTheBlog(Blog blog) =>
        [
            TestObjectFactory.NewPost().WithTitle("Post1").WithContent("Post 1 content").WithBlog(blog).Build(),
            TestObjectFactory.NewPost().WithTitle("Post2").WithContent("Post 2 content").WithBlog(blog).Build()
        ];
    }
}
