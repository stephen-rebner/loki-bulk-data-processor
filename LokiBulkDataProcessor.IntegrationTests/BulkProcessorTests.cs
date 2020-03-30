﻿using NUnit.Framework;
using FluentAssertions;
using LokiBulkDataProcessor.IntegrationTests.Abstract;
using LokiBulkDataProcessor.IntegrationTests.TestModels;
using LokiBulkDataProcessor.IntegrationTests.TestObjectBuilders;
using Loki.BulkDataProcessor.Commands.Factory;
using System.Collections.Generic;
using Loki.BulkDataProcessor;
using System.Threading.Tasks;
using System.Linq;
using LokiBulkDataProcessor.IntegrationTests.TestModels.Dtos;
using System.Data;

namespace LokiBulkDataProcessor.IntegrationTests
{
    public class BulkProcessorTests : BaseIntegrationTest
    {
        private const string Post1 = "Post1";
        private const string Post2 = "Post2";
        private const string PostContent1 = "PostContent2";
        private const string PostContent2 = "PostContent2";

        private BulkProcessor _bulkProcessor;

        [SetUp]
        public void Setup()
        {
            _bulkProcessor = new BulkProcessor("Server=(local);Database=IntegrationTestsDb;Trusted_Connection=True;MultipleActiveResultSets=true", 
                new CommandFactory());
        }

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

            await _bulkProcessor.SaveAsync(models, nameof(TestDbContext.TestDbModels));

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

            await _bulkProcessor.SaveAsync(datatable, nameof(TestDbContext.TestDbModels));

            var results = TestDbContext.TestDbModels.OrderBy(x => x.Id).ToList();

            results.Should().BeEquivalentTo(expctedResults);
        }

        [Test]
        public async Task SaveAsync_ShouldSaveModelsSuccessfully_WhenTableHasAForeignKey()
        {
            var blog = GivenThisBlog();

            var postDtos = AndGivenThesePostDtosAssociatedToBlogId(blog.Id);

            await WhenSaveAsyncIsCalled(postDtos, nameof(TestDbContext.Posts));

            var posts = ThesePostsWithThisBlog(postDtos, blog);

            await ShouldExistInTheDatabase(posts);
        }

        [Test]
        public async Task SaveAsync_ShouldSaveDataTableSuccessfully_WhenTableHasAForeignKey()
        {
            var blog = GivenThisBlog();

            var postDataTable = AndGivenThisPostDataTableAssociatedToBlogId(blog.Id);

            await WhenSaveAsyncIsCalled(postDataTable, nameof(TestDbContext.Posts));
            var posts = ThesePostsWithThisBlog(postDataTable, blog);

            await ShouldExistInTheDatabase(posts);
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

            var postDtos = new PostDto[] { post1, post2 };

            return postDtos;
        }

        private async Task WhenSaveAsyncIsCalled<T>(IEnumerable<T> dataToCopy, string tableName) where T : class
        {
            await _bulkProcessor.SaveAsync(dataToCopy, tableName);
        }

        private async Task WhenSaveAsyncIsCalled(DataTable dataToCopy, string tableName)
        {
            await _bulkProcessor.SaveAsync(dataToCopy, tableName);
        }

        private IEnumerable<Post> ThesePostsWithThisBlog(IEnumerable<PostDto> postDtos, Blog blog)
        {
            var posts = new List<Post>();

            var currentPostId = TestDbContext.Posts.Min(post => post.Id);

            foreach(var postDto in postDtos)
            {
                var newPost = TestObjectFactory.NewPost()
                .BuildFromPostDto(postDto)
                .WithBlog(blog)
                .WithId(currentPostId)
                .Build();

                posts.Add(newPost);

                currentPostId = currentPostId+1;
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
                .WithContent((string)postDataRow["Content"])
                .WithTitle((string)postDataRow["Title"])
                .WithBlog(blog)
                .WithId(currentPostId)
                .Build();

                posts.Add(newPost);

                currentPostId = currentPostId + 1;
            }

            return posts;
        }

        private async Task ShouldExistInTheDatabase<T>(IEnumerable<T> expectedPosts)
        {
            var actualPosts = await LoadAllEntities<Post>();

            expectedPosts.Should().BeEquivalentTo(actualPosts);
        }

        private DataTable AndGivenThisPostDataTableAssociatedToBlogId(int blogId)
        {
            return TestObjectFactory.NewPostDataTable()
                .WithRowData(blogId, Post1, PostContent1)
                .WithRowData(blogId, Post2, PostContent2)
                .Build();
        }
    }
}
