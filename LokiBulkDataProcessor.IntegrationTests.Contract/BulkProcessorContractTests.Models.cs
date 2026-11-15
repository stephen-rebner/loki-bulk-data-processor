using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels.Dtos;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>Saving <c>IEnumerable&lt;T&gt;</c> models, with and without a ModelMapping.</summary>
public abstract partial class BulkProcessorContractTests
{
    [Test]
    public async Task SaveAsync_ShouldSaveModelsSuccessfully_WhenTableHasNoForeignKey()
    {
        TestDbModel[] models = ContractRows.ThreeMixedNullTestDbModels();

        await BulkProcessor.SaveAsync(models, TestDbModelsTable);

        await TheTestDbModelsShouldBe(models);
    }

    [Test]
    public async Task SaveAsync_ShouldSaveModelsAndIgnoreGetOnlyProperties_WhenModelHasAGetOnlyProperty()
    {
        var models = new List<TestDbModelWithGetOnlyProperty>
        {
            new() { Id = 1, StringColumn = "String Value 1", BoolColumn = true, DateColumn = ContractRows.Date1 },
            new() { Id = 2, StringColumn = "String Value 2", BoolColumn = false, DateColumn = ContractRows.Date2, NullableBoolColumn = true }
        };

        await BulkProcessor.SaveAsync(models, TestDbModelsTable);

        (await ReadTestDbModelsAsync()).Should().BeEquivalentTo(models, options => options.ExcludingMissingMembers());
    }

    [Test]
    public async Task SaveAsync_ShouldSaveModelsSuccessfully_WhenTableHasAForeignKey_AndUsesAMapper()
    {
        int blogId = await GivenABlogAsync();

        await BulkProcessor.SaveAsync(ContractRows.PostDtos(blogId), PostsTable);

        await ThePostsShouldBe((blogId, "Post1", "Post 1 content"), (blogId, "Post2", "Post 2 content"));
    }

    [Test]
    public async Task SaveAsync_ShouldSaveModelsSuccessfully_WhenUsingColumnMappingsMappedToSameColumns()
    {
        int blogId = await GivenABlogAsync();

        await BulkProcessor.SaveAsync(ContractRows.Posts(blogId), PostsTable);

        await ThePostsShouldBe((blogId, "Post1", "Post 1 content"), (blogId, "Post2", "Post 2 content"));
    }

    [Test]
    public async Task SaveAsync_ShouldPreserveNulls_WhenNullableColumnsHaveNoValue()
    {
        TestDbModel withNulls = ContractRows.FullTestDbModel(1, null, ContractRows.Date1, true, null, null);
        using var tableWithNulls = ContractRows.TestDbModelsDataTable(ContractRows.FullTestDbModel(2, null, ContractRows.Date2, false, null, null));

        await BulkProcessor.SaveAsync(new[] { withNulls }, TestDbModelsTable);
        await BulkProcessor.SaveAsync(tableWithNulls, TestDbModelsTable);

        (await ReadTestDbModelsAsync()).Should().HaveCount(2).And.OnlyContain(model =>
            model.StringColumn == null && model.NullableBoolColumn == null && model.NullableDateColumn == null);
    }

    [Test]
    public async Task SaveAsync_ShouldThrowOperationCanceledAndSaveNothing_WhenTheTokenIsAlreadyCancelled()
    {
        Func<Task> action = () => BulkProcessor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable, new System.Threading.CancellationToken(canceled: true));

        await action.Should().ThrowAsync<OperationCanceledException>();
        await TheTestDbModelsTableShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldSaveEveryRow_WhenTheBatchSizeIsSmallerThanTheRowCount()
    {
        BulkProcessor.BatchSize = 2;
        TestDbModel[] models = { ContractRows.TestDbModel(1, "Row 1"), ContractRows.TestDbModel(2, "Row 2"),
            ContractRows.TestDbModel(3, "Row 3"), ContractRows.TestDbModel(4, "Row 4"), ContractRows.TestDbModel(5, "Row 5") };

        await BulkProcessor.SaveAsync(models, TestDbModelsTable);

        await TheTestDbModelValuesShouldBe("Row 1", "Row 2", "Row 3", "Row 4", "Row 5");
    }
}
