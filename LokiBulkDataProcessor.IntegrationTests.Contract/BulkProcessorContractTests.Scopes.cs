using System.Data;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// Several saves through one processor (one web request / DI scope), with Loki's own transaction per save, and inside
/// one caller transaction that is then committed or rolled back.
/// </summary>
public abstract partial class BulkProcessorContractTests
{
    /// <summary>The input shape a scope test saves its rows through.</summary>
    public enum InputShape
    {
        Models,
        DataTable,
        DataReader,
        Json
    }

    [Test]
    public Task WebApiScenario_MultipleSaveOperations_WithinSingleScope() =>
        TwoSavesShouldBeCommittedAsync(InputShape.Models, "API Test");

    [Test]
    public Task WebApiScenario_MultipleDataTableSaves_WithinSingleScope() =>
        TwoSavesShouldBeCommittedAsync(InputShape.DataTable, "API DataTable");

    [Test]
    public Task WebApiScenario_MultipleDataReaderSaves_WithinSingleScope() =>
        TwoSavesShouldBeCommittedAsync(InputShape.DataReader, "API Reader");

    [Test]
    public Task WebApiScenario_MultipleJsonStreamSaves_WithinSingleScope() =>
        TwoSavesShouldBeCommittedAsync(InputShape.Json, "API Stream");

    [Test]
    public Task WebApiScenario_MultipleSaveOperations_WithinSingleScope_WithExternalTransaction() =>
        TwoSavesInACallerTransactionShouldFollowItAsync(InputShape.Models, "API Transaction Test", commit: true);

    [Test]
    public Task WebApiScenario_MultipleDataTableSaves_WithinSingleScope_WithExternalTransaction() =>
        TwoSavesInACallerTransactionShouldFollowItAsync(InputShape.DataTable, "API DataTable Tx", commit: true);

    [Test]
    public Task WebApiScenario_MultipleDataReaderSaves_WithinSingleScope_WithExternalTransaction() =>
        TwoSavesInACallerTransactionShouldFollowItAsync(InputShape.DataReader, "API Reader Tx", commit: true);

    [Test]
    public Task WebApiScenario_MultipleJsonStreamSaves_WithinSingleScope_WithExternalTransaction() =>
        TwoSavesInACallerTransactionShouldFollowItAsync(InputShape.Json, "API Stream Tx", commit: true);

    [Test]
    public Task WebApiScenario_MultipleSaveOperations_WithinSingleScope_WithExternalTransaction_WhenRolledBack() =>
        TwoSavesInACallerTransactionShouldFollowItAsync(InputShape.Models, "API Transaction Rollback", commit: false);

    [Test]
    public Task WebApiScenario_MultipleDataTableSaves_WithinSingleScope_WithExternalTransaction_WhenRolledBack() =>
        TwoSavesInACallerTransactionShouldFollowItAsync(InputShape.DataTable, "API DataTable Rollback", commit: false);

    [Test]
    public Task WebApiScenario_MultipleDataReaderSaves_WithinSingleScope_WithExternalTransaction_WhenRolledBack() =>
        TwoSavesInACallerTransactionShouldFollowItAsync(InputShape.DataReader, "API Reader Rollback", commit: false);

    [Test]
    public Task WebApiScenario_MultipleJsonStreamSaves_WithinSingleScope_WithExternalTransaction_WhenRolledBack() =>
        TwoSavesInACallerTransactionShouldFollowItAsync(InputShape.Json, "API Stream Rollback", commit: false);

    private async Task TwoSavesShouldBeCommittedAsync(InputShape shape, string valuePrefix)
    {
        await SaveTwoRowsOneAtATimeAsync(shape, valuePrefix);

        await TheTestDbModelValuesShouldBe($"{valuePrefix} 1", $"{valuePrefix} 2");
    }

    private async Task TwoSavesInACallerTransactionShouldFollowItAsync(InputShape shape, string valuePrefix, bool commit)
    {
        await using (DbConnection connection = await OpenConnectionAsync())
        await using (DbTransaction transaction = await connection.BeginTransactionAsync())
        {
            BulkProcessor.Transaction = transaction;
            await SaveTwoRowsOneAtATimeAsync(shape, valuePrefix);
            await CompleteAsync(transaction, commit);
        }

        await (commit ? TheTestDbModelValuesShouldBe($"{valuePrefix} 1", $"{valuePrefix} 2") : TheTestDbModelsTableShouldBeEmpty());
    }

    private async Task SaveTwoRowsOneAtATimeAsync(InputShape shape, string valuePrefix)
    {
        await SaveOneRowAsync(shape, ContractRows.FullTestDbModel($"{valuePrefix} 1", ContractRows.Date1, true, null, null));
        await SaveOneRowAsync(shape, ContractRows.FullTestDbModel($"{valuePrefix} 2", ContractRows.Date2, false, true, new System.DateTime(2020, 01, 19)));
    }

    private async Task SaveOneRowAsync(InputShape shape, TestDbModel row)
    {
        switch (shape)
        {
            case InputShape.Models:
                await BulkProcessor.SaveAsync(new[] { row }, TestDbModelsTable);
                break;
            case InputShape.DataTable:
                using (DataTable table = ContractRows.TestDbModelsDataTable(row)) { await BulkProcessor.SaveAsync(table, TestDbModelsTable); }
                break;
            case InputShape.DataReader:
                using (IDataReader reader = ContractRows.TestDbModelsReader(row)) { await BulkProcessor.SaveAsync(reader, TestDbModelsTable); }
                break;
            default:
                using (Stream json = ContractRows.TestDbModelsJson(TestDbModelsTable, row)) { await BulkProcessor.SaveAsync(json); }
                break;
        }
    }
}
