using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
            new() { StringColumn = "String Value 1", BoolColumn = true, DateColumn = ContractRows.Date1 },
            new() { StringColumn = "String Value 2", BoolColumn = false, DateColumn = ContractRows.Date2, NullableBoolColumn = true }
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
        TestDbModel withNulls = ContractRows.FullTestDbModel(null, ContractRows.Date1, true, null, null);
        using var tableWithNulls = ContractRows.TestDbModelsDataTable(ContractRows.FullTestDbModel(null, ContractRows.Date2, false, null, null));

        await BulkProcessor.SaveAsync(new[] { withNulls }, TestDbModelsTable);
        await BulkProcessor.SaveAsync(tableWithNulls, TestDbModelsTable);

        (await ReadTestDbModelsAsync()).Should().HaveCount(2).And.OnlyContain(model =>
            model.StringColumn == null && model.NullableBoolColumn == null && model.NullableDateColumn == null);
    }

    /// <summary>KeepNulls: NullableBoolColumn has <c>DEFAULT 1</c>, so a provider that let the default win would read back true.</summary>
    [Test]
    public async Task SaveAsync_ShouldKeepAnExplicitNull_WhenTheNullableColumnHasADefault()
    {
        TestDbModel withNull = ContractRows.FullTestDbModel("explicit null", ContractRows.Date1, true, nullableFlag: null, nullableDate: null);
        using DataTable tableWithNull = ContractRows.TestDbModelsDataTable(withNull);

        await BulkProcessor.SaveAsync(new[] { withNull }, TestDbModelsTable);
        await BulkProcessor.SaveAsync(tableWithNull, TestDbModelsTable);

        (await ReadTestDbModelsAsync()).Should().HaveCount(2).And.OnlyContain(model => model.NullableBoolColumn == null,
            "an explicit null must be kept, not replaced by the column's DEFAULT");
    }

    /// <summary>Control for the KeepNulls test: proves the DEFAULT exists, so that test cannot pass vacuously.</summary>
    [Test]
    public async Task SaveAsync_ShouldApplyTheColumnDefault_WhenTheSourceDoesNotHaveTheColumn()
    {
        using DataTable withoutNullableBool = ContractRows.TestDbModelsDataTable(ContractRows.TestDbModel("defaulted"));
        withoutNullableBool.Columns.Remove("NullableBoolColumn");

        await NewProcessorWithoutMappings().SaveAsync(withoutNullableBool, TestDbModelsTable);

        (await ReadTestDbModelsAsync()).Single().NullableBoolColumn.Should().BeTrue("NullableBoolColumn has DEFAULT 1 (true)");
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
        TestDbModel[] models = { ContractRows.TestDbModel("Row 1"), ContractRows.TestDbModel("Row 2"),
            ContractRows.TestDbModel("Row 3"), ContractRows.TestDbModel("Row 4"), ContractRows.TestDbModel("Row 5") };

        await BulkProcessor.SaveAsync(models, TestDbModelsTable);

        await TheTestDbModelValuesShouldBe("Row 1", "Row 2", "Row 3", "Row 4", "Row 5");
    }
}
