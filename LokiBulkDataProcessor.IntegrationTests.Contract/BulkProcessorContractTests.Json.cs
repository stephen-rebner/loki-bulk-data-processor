using System;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// Saving JSON payloads, including the security options: the caller chooses the destination, opt-in
/// RequireDataMappingForJson and MaxJsonPayloadBytes.
/// </summary>
public abstract partial class BulkProcessorContractTests
{
    [Test]
    public async Task SaveAsync_ShouldSaveJsonStreamSuccessfully_WhenJsonFollowsSchema()
    {
        TestDbModel[] rows = ContractRows.TwoMixedNullTestDbModels();
        using Stream json = ContractRows.TestDbModelsJson(TestDbModelsTable, rows);

        await BulkProcessor.SaveAsync(json);

        await TheTestDbModelsShouldBe(rows);
    }

    [Test]
    public async Task SaveAsync_ShouldSaveJsonStreamSuccessfully_WhenCommitingExternalTransaction()
    {
        TestDbModel[] rows = ContractRows.TwoMixedNullTestDbModels();

        await SaveJsonInsideACallerTransactionAsync(rows, commit: true);

        await TheTestDbModelsShouldBe(rows);
    }

    [Test]
    public async Task SaveAsync_ShouldNotSaveData_WhenRollingBackExternalTransaction()
    {
        await SaveJsonInsideACallerTransactionAsync(ContractRows.TwoMixedNullTestDbModels(), commit: false);

        await TheTestDbModelsTableShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldWriteToTheCallersDestination_WhenTheJsonPayloadNamesAnotherTable()
    {
        using Stream json = ContractRows.TestDbModelsJson(MissingTable, ContractRows.TwoTestDbModels());

        await BulkProcessor.SaveAsync(json, TestDbModelsTable);

        await TheTestDbModelValuesShouldBe("One", "Two");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowAndSaveNothing_WhenAMappingIsRequiredAndTheJsonDestinationHasNone()
    {
        using ServiceProvider services = BuildServices(options => { AddContractMappings(options); options.RequireDataMappingForJson = true; });
        using Stream json = ContractRows.TestDbModelsJson(TestDbModelsTable, ContractRows.TwoTestDbModels());

        Func<Task> action = () => CreateProcessor(services).SaveAsync(json, TestDbModelsTable);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*RequireDataMappingForJson*");
        await TheTestDbModelsTableShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldUseTheDestinationsDataMapping_WhenAMappingIsRequiredAndOneIsRegistered()
    {
        using ServiceProvider services = BuildServices(options => { AddContractMappings(options); options.RequireDataMappingForJson = true; });
        int blogId = await GivenABlogAsync();
        using Stream json = ContractRows.MappedPostsJson("SomethingElse", blogId, "Mapped title");

        await CreateProcessor(services).SaveAsync(json, PostsTable);

        await ThePostTitlesShouldBe("Mapped title");
    }

    [Test]
    public async Task SaveAsync_ShouldThrowInvalidDataExceptionAndSaveNothing_WhenTheJsonPayloadIsOverTheLimit()
    {
        using ServiceProvider services = BuildServices(options => { AddContractMappings(options); options.MaxJsonPayloadBytes = 64; });
        using Stream json = ContractRows.TestDbModelsJson(TestDbModelsTable, ContractRows.TwoTestDbModels());

        Func<Task> action = () => CreateProcessor(services).SaveAsync(json, TestDbModelsTable);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*MaxJsonPayloadBytes limit of 64 bytes*");
        await TheTestDbModelsTableShouldBeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldSaveTheJson_WhenThePayloadIsWithinTheLimit()
    {
        using ServiceProvider services = BuildServices(options => { AddContractMappings(options); options.MaxJsonPayloadBytes = 1024 * 1024; });
        using Stream json = ContractRows.TestDbModelsJson(TestDbModelsTable, ContractRows.TwoTestDbModels());

        await CreateProcessor(services).SaveAsync(json);

        await TheTestDbModelValuesShouldBe("One", "Two");
    }

    private async Task SaveJsonInsideACallerTransactionAsync(TestDbModel[] rows, bool commit)
    {
        await using DbConnection connection = await OpenConnectionAsync();
        await using DbTransaction transaction = await connection.BeginTransactionAsync();
        using Stream json = ContractRows.TestDbModelsJson(TestDbModelsTable, rows);
        BulkProcessor.Transaction = transaction;

        await BulkProcessor.SaveAsync(json);

        await CompleteAsync(transaction, commit);
    }
}
