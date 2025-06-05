using System;
using FluentAssertions;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.TestModels;
using LokiBulkDataProcessor.IntegrationTests.TestModels.Dtos;
using LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders;
using NUnit.Framework;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using FastMember;

namespace LokiBulkDataProcessor.IntegrationTests
{
    public class BulkProcessorTests : BaseIntegrationTest
    {
        private const string Post1 = "Post1";
        private const string Post2 = "Post2";
        private const string PostContent1 = "PostContent2";
        private const string PostContent2 = "PostContent2";

        [Test]
        public async Task SaveAsync_ShouldSaveModelsSuccessfully_WhenTableHasNoForeignKey()
        {
            var model1 = TestObjectFactory.TestDbModelObject()
                .WithId(1)
                .WithStringColumnValue("String Value 1")
                .WithDateColumnValue(new System.DateTime(2020, 01, 26))
                .WithBoolColumnValue(true)
                .WithNullableBoolColumnValue(null)
                .WithNullableDateColumnValue(null)
                .Build();

            var model2 = TestObjectFactory.TestDbModelObject()
                .WithId(2)
                .WithStringColumnValue("String Value 2")
                .WithDateColumnValue(new System.DateTime(2020, 01, 27))
                .WithBoolColumnValue(false)
                .WithNullableBoolColumnValue(true)
                .WithNullableDateColumnValue(new System.DateTime(2020, 01, 19))
                .Build();

            var model3 = TestObjectFactory.TestDbModelObject()
                 .WithId(3)
                 .WithStringColumnValue("String Value 3")
                 .WithDateColumnValue(new System.DateTime(2020, 01, 28))
                 .WithBoolColumnValue(true)
                 .WithNullableBoolColumnValue(false)
                 .WithNullableDateColumnValue(new System.DateTime(2020, 1, 10))
                 .Build();

            var models = new List<TestDbModel> { model1, model2, model3 };

            await BulkProcessor.SaveAsync(models, nameof(TestDbContext.TestDbModels));

            var results = TestDbContext.TestDbModels.OrderBy(x => x.Id).ToList();

            results.Should().BeEquivalentTo(models);
        }

        [Test]
        public async Task SaveAsync_ShouldSaveDataTableSuccessfully_WhenTableHasNoForeignKey()
        {
            using var datatable = TestObjectFactory.NewTestDataTable()
                .WithRowData(1, "String Value 1", true, new System.DateTime(2020, 01, 26), null, null)
                .WithRowData(2, "String Value 2", false, new System.DateTime(2020, 01, 27), true, new System.DateTime(2020, 01, 19))
                .Build();

            var exptectedModel1 = TestObjectFactory.TestDbModelObject()
                .WithId(1)
                .WithStringColumnValue("String Value 1")
                .WithDateColumnValue(new System.DateTime(2020, 01, 26))
                .WithBoolColumnValue(true)
                .WithNullableBoolColumnValue(null)
                .WithNullableDateColumnValue(null)
                .Build();

            var expectedModel2 = TestObjectFactory.TestDbModelObject()
                .WithId(2)
                .WithStringColumnValue("String Value 2")
                .WithDateColumnValue(new System.DateTime(2020, 01, 27))
                .WithBoolColumnValue(false)
                .WithNullableBoolColumnValue(true)
                .WithNullableDateColumnValue(new System.DateTime(2020, 01, 19))
                .Build();

            var expctedResults = new List<TestDbModel> { exptectedModel1, expectedModel2 };

            await BulkProcessor.SaveAsync(datatable, nameof(TestDbContext.TestDbModels));

            var results = TestDbContext.TestDbModels.OrderBy(x => x.Id).ToList();

            results.Should().BeEquivalentTo(expctedResults);
        }

        [Test]
        public async Task SaveAsync_ShouldSaveModelsSuccessfully_WhenTableHasAForeignKey_AndUsesAMapper()
        {
            var blog = GivenThisBlog();

            var postDtos = AndGivenThesePostDtosAssociatedToBlogId(blog.Id);

            await WhenSaveAsyncIsCalled(postDtos, nameof(TestDbContext.Posts));

            var posts = ThesePostsWithThisBlog(postDtos, blog);

            await ShouldExistInTheDatabase(posts);
        }

        [Test]
        public async Task SaveAsync_ShouldSaveDataTableSuccessfully_WhenTableHasAForeignKey_AndHasNoMapping()
        {
            var blog = GivenThisBlog();

            var postDataTable = AndGivenAPostDataTableWithNoMapping(blog.Id);

            await WhenSaveAsyncIsCalled(postDataTable, nameof(TestDbContext.Posts));
            var posts = ThesePostsWithThisBlog(postDataTable, blog);

            await ShouldExistInTheDatabase(posts);
        }

        [Test]
        public async Task SaveAsync_ShouldSaveDataTableSuccessfully_WhenTableHasAForeignKey_AndHasAMapping()
        {
            var blog = GivenThisBlog();

            var postDataTable = AndGivenAPostDataTableWithAMapping(blog.Id);

            await WhenSaveAsyncIsCalled(postDataTable, nameof(TestDbContext.Posts));
            var posts = ThesePostsWithThisBlog(postDataTable, blog);

            await ShouldExistInTheDatabase(posts);
        }

        [Test]
        public async Task SaveAsync_ShouldSaveModelsSuccessfully_WhenUsingColumnMappingsMappedToSameColumns()
        {
            var blog = GivenThisBlog();

            var posts = AndGivenThesePostsAssociatedToTheBlog(blog);

            await WhenSaveAsyncIsCalled(posts, nameof(TestDbContext.Posts));

            var expectedPosts = ThesePostsWithThisBlog(posts, blog);

            await ShouldExistInTheDatabase(expectedPosts);
        }

        [Test]
        public async Task SaveAsync_ShouldSavePostsSuccessfully_WhenCommitingExternalTransaction()
        {
            using var sqlConnection = await WhenUsingAnExternalSqlConnection();
            using var transaction = await sqlConnection.BeginTransactionAsync();

            var blog = GivenThisBlog();

            var posts = AndGivenThesePostsAssociatedToTheBlog(blog);

            WhenAnExternalTransactionIsPassedToTheBulkProcessor(transaction);

            await WhenSaveAsyncIsCalled(posts, nameof(TestDbContext.Posts));

            AndTheTransactionIsCommited(transaction);

            var expectedPosts = ThesePostsWithThisBlog(posts, blog);

            await ShouldExistInTheDatabase(expectedPosts);
        }

        [Test]
        public async Task SaveAsync_ShouldNotSavePosts_WhenRollingBackExternalTransaction()
        {
            using var sqlConnection = await WhenUsingAnExternalSqlConnection();
            using var transaction = sqlConnection.BeginTransaction();

            var blog = GivenThisBlog();

            var posts = AndGivenThesePostsAssociatedToTheBlog(blog);

            WhenAnExternalTransactionIsPassedToTheBulkProcessor(transaction);

            await WhenSaveAsyncIsCalled(posts, nameof(TestDbContext.Posts));

            AndTheTransactionIsRolledBack(transaction);

            await TheDatabaseTableShouldBeEmpty<Post>();
        }

        [Test]
        public async Task SaveAsync_ShouldBulkCopyFromDataReader_WhenUsingAMapping()
        {
            // arrange
            var blog = GivenThisBlog();
            
            var postDtos = AndGivenThesePostDtosAssociatedToBlogId(blog.Id);

            var columnNames = new[]
            {
                "ATitle",
                "ContentA",
                "ABlogId"
            };

            // create a data reader from the post dtos
            using var reader = ObjectReader.Create(postDtos, columnNames);
            
            // act
            await WhenSaveAsyncIsCalled(reader, nameof(TestDbContext.Posts));
            
            // assert
            var expectedPosts = ThesePostsWithThisBlog(postDtos, blog);
            
            await ShouldExistInTheDatabase(expectedPosts);
        }
        
        [Test]
        public async Task SaveAsync_ShouldBulkCopyFromDataReader_WhenUsingNotAMapping()
        {
            // arrange
            CreateLokiBulkDataProcessorWithNoMappings();
            
            var blog = GivenThisBlog();
            
            var posts = AndGivenThesePostDtosWithNoMappingsAssociatedToBlogId(blog.Id);

            var columnNames = new[]
            {
                "Title",
                "Content",
                "BlogId"
            };
            
            // create a data reader from the post dtos
            using var reader = ObjectReader.Create(posts, columnNames);
            
            // act
            await WhenSaveAsyncIsCalled(reader, nameof(TestDbContext.Posts));
            
            // assert
            var expectedPosts = ThesePostsWithThisBlog(posts, blog);
            
            await ShouldExistInTheDatabase(expectedPosts);
        }
        
        [Test]
        public async Task SaveAsync_ShouldBulkCopyFromDataReader_WhenUsingNotAMappingAndUsingATransaction()
        {
            // arrange
            CreateLokiBulkDataProcessorWithNoMappings();
            
            using var sqlConnection = await WhenUsingAnExternalSqlConnection();
            using var transaction = await sqlConnection.BeginTransactionAsync();
            
            var blog = GivenThisBlog();

            var posts = AndGivenThesePostDtosWithNoMappingsAssociatedToBlogId(blog.Id);

            var columnNames = new[]
            {
                "Title",
                "Content",
                "BlogId"
            };

            // act
            using var reader = ObjectReader.Create(posts, columnNames);

            WhenAnExternalTransactionIsPassedToTheBulkProcessor(transaction);

            await WhenSaveAsyncIsCalled(reader, nameof(TestDbContext.Posts));
            
            await transaction.CommitAsync();
            
            // assert
            var expectedPosts = ThesePostsWithThisBlog(posts, blog);

            await ShouldExistInTheDatabase(expectedPosts);
        }

        private Blog GivenThisBlog()
        {
            var blog = TestObjectFactory.NewBlog()
                .WithUrl("http://a-url.com")
                .Build();

            SaveEntities(blog);

            return blog;
        }
        
        private PostDto[] AndGivenThesePostDtosAssociatedToBlogId(int blogId)
        {
            var post1 = TestObjectFactory.NewPostDto()
                .WithTitle("Post1")
                .WithContent("Post 1 content")
                .WithBlogId(blogId)
                .Build();

            var post2 = TestObjectFactory.NewPostDto()
                .WithTitle("Post2")
                .WithContent("Post 2 content")
                .WithBlogId(blogId)
                .Build();

            return new PostDto[] { post1, post2 };
        }
        
        private PostDtoWithNoMapping[] AndGivenThesePostDtosWithNoMappingsAssociatedToBlogId(int blogId)
        {
            var post1 = new PostDtoWithNoMappingBuilder()
                .WithTitle("Post1")
                .WithContent("Post 1 content")
                .WithBlogId(blogId)
                .Build();

            var post2 = new PostDtoWithNoMappingBuilder()
                .WithTitle("Post2")
                .WithContent("Post 2 content")
                .WithBlogId(blogId)
                .Build();

            return [post1, post2];
        }

        private Post[] AndGivenThesePostsAssociatedToTheBlog(Blog blog)
        {
            var post1 = TestObjectFactory.NewPost()
                .WithTitle("Post1")
                .WithContent("Post 1 content")
                .WithBlog(blog)
                .Build();

            var post2 = TestObjectFactory.NewPost()
                .WithTitle("Post2")
                .WithContent("Post 2 content")
                .WithBlog(blog)
                .Build();

            return new Post[] { post1, post2 };
        }

        private async Task<SqlConnection> WhenUsingAnExternalSqlConnection()
        {
            var sqlConnection = new SqlConnection(base.GetConnectionString());
            await sqlConnection.OpenAsync();

            return sqlConnection;
        }

        private void WhenAnExternalTransactionIsPassedToTheBulkProcessor(IDbTransaction transaction)
        {
            BulkProcessor.Transaction = transaction;
        }

        private void AndTheTransactionIsCommited(IDbTransaction transaction)
        {
            transaction.Commit();
        }

        private void AndTheTransactionIsRolledBack(IDbTransaction transaction)
        {
            transaction.Rollback();
        }

        private async Task WhenSaveAsyncIsCalled<T>(IEnumerable<T> dataToCopy, string tableName) where T : class
        {
            await BulkProcessor.SaveAsync(dataToCopy, tableName);
        }

        private async Task WhenSaveAsyncIsCalled(DataTable dataToCopy, string tableName)
        {
            await BulkProcessor.SaveAsync(dataToCopy, tableName);
        }
        
        private async Task WhenSaveAsyncIsCalled(IDataReader dataReader, string tableName)
        {
            await BulkProcessor.SaveAsync(dataReader, tableName);
        }

        private IEnumerable<Post> ThesePostsWithThisBlog(IEnumerable<PostDto> postDtos, Blog blog)
        {
            var posts = new List<Post>();

            var currentPostId = TestDbContext.Posts.Min(post => post.Id);

            foreach (var postDto in postDtos)
            {
                var newPost = TestObjectFactory.NewPost()
                .BuildFromPostDto(postDto)
                .WithBlog(blog)
                .WithId(currentPostId)
                .Build();

                posts.Add(newPost);

                currentPostId += 1;
            }

            return posts;
        }

        private IEnumerable<Post> ThesePostsWithThisBlog(IEnumerable<PostDtoWithNoMapping> postDtos, Blog blog)
        {
            var posts = new List<Post>();

            var currentPostId = TestDbContext.Posts.Min(post => post.Id);

            foreach (var postDto in postDtos)
            {
                var newPost = TestObjectFactory.NewPost()
                .BuildFromPostDtoWithNoMapping(postDto)
                .WithBlog(blog)
                .WithId(currentPostId)
                .Build();

                posts.Add(newPost);

                currentPostId += 1;
            }

            return posts;
        }

        private IEnumerable<Post> ThesePostsWithThisBlog(IEnumerable<Post> posts, Blog blog)
        {
            var currentPostId = TestDbContext.Posts.Min(post => post.Id);

            foreach (var post in posts)
            {
                post.Id = currentPostId;

                currentPostId += 1;
            }

            return posts;
        }

        private IEnumerable<Post> ThesePostsWithThisBlog(DataTable postDataTable, Blog blog)
        {
            var posts = new List<Post>();

            var currentPostId = TestDbContext.Posts.Min(post => post.Id);

            foreach (DataRow postDataRow in postDataTable.Rows)
            {
                var newPost = TestObjectFactory.NewPost()
                .WithContent((string)postDataRow[2])
                .WithTitle((string)postDataRow[1])
                .WithBlog(blog)
                .WithId(currentPostId)
                .Build();

                posts.Add(newPost);

                currentPostId += 1;
            }

            return posts;
        }

        private DataTable AndGivenAPostDataTableWithNoMapping(int blogId)
        {
            return TestObjectFactory.NewPostDataTable()
                .Create()
                .WithDefaultColumnNames()
                .WithRowData(blogId, Post1, PostContent1)
                .WithRowData(blogId, Post2, PostContent2)
                .Build();
        }

        private DataTable AndGivenAPostDataTableWithAMapping(int blogId)
        {
            return TestObjectFactory.NewPostDataTable()
                .Create()
                .WithTableName("Posts")
                .WithCustomColumnNames("ATitle", "ContentA", "ABlogId")
                .WithRowData(blogId, Post1, PostContent1)
                .WithRowData(blogId, Post2, PostContent2)
                .Build();
        }
    }
}
