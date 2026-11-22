using System.Data;
using Loki.BulkDataProcessor;
using Loki.Samples.WebApi.Engines;
using Loki.Samples.WebApi.Imports;
using Oracle.ManagedDataAccess.Client;

namespace Loki.Samples.WebApi.Staging;

/// <summary>
/// The Oracle staging import: (1) a direct-path load (<c>OracleWriteMode.DirectPath</c>, registered as "Oracle.DirectPath") into
/// the staging table STG_PRODUCTS, committed by Loki's own transaction, then (2) one set-based, conventional
/// <c>INSERT ... SELECT</c> into Products plus the removal of the batch from staging, in one transaction of this code.
/// </summary>
/// <remarks>
/// DIRECT PATH IS FOR STAGING TABLES ONLY (README "Oracle direct path"). It skips checks the default array bind performs, so this
/// sample keeps every hazard the README lists out of reach:
/// <list type="bullet">
/// <item>The table name is a constant (never from a request or a JSON payload).</item>
/// <item>No processor.Transaction: Loki's own transaction commits right after the load, so other sessions' queries on the table
/// wait only for the load (hazard 1), and there is exactly one load per transaction (hazard 7, ORA-39822).</item>
/// <item>STG_PRODUCTS has no primary or unique key and no index: duplicate keys cannot make an index UNUSABLE (hazards 3, 5). Keys
/// and foreign keys are enforced in step 2, which uses a conventional INSERT into Products (hazard 2).</item>
/// <item>No trigger on STG_PRODUCTS (hazard 6, ORA-26086), and the load's transaction never reads the table (hazard 4).</item>
/// <item>The load is synchronous (hazard 8): it blocks this request's thread until it finishes, and the token is checked only
/// before it starts.</item>
/// </list>
/// </remarks>
internal sealed class OracleStagingImportService(EngineCatalog engines, ILogger<OracleStagingImportService> logger)
{
    // Constant SQL; the only value, the batch id, is a bind variable.
    private const string MoveBatchToProducts =
        "INSERT INTO Products (Sku, Name, Price) SELECT Sku, Name, Price FROM STG_PRODUCTS WHERE BatchId = :batchId";

    // DELETE (not TRUNCATE) clears staging: it removes only this batch, so concurrent imports keep theirs, and it is part of
    // the move's transaction, so the batch leaves staging exactly when it lands in Products. TRUNCATE is DDL: it would
    // commit on its own and empty every batch. Direct-path loads write above the table's high-water mark and never reuse the
    // space DELETE frees, so empty the table with `TRUNCATE TABLE STG_PRODUCTS DROP ALL STORAGE` from time to time while no
    // import runs (the README also requires that after hazard 4).
    private const string DeleteBatchFromStaging = "DELETE FROM STG_PRODUCTS WHERE BatchId = :batchId";

    public async Task<StagingImportResult> ImportProductsAsync(int count, bool includeDuplicate, CancellationToken ct)
    {
        string batchId = Guid.NewGuid().ToString("N");
        using DataTable rows = StagedProductRows.Create(batchId, RequestGuards.ThrowIfNotARowCount(count, nameof(count)), includeDuplicate);
        await LoadIntoStagingAsync(rows, ct);
        int moved = await MoveIntoProductsAsync(batchId, ct);
        return new StagingImportResult(batchId, rows.Rows.Count, moved);
    }

    /// <summary>Step 1: direct path, in Loki's own transaction (committed when the save returns, rolled back if it fails).</summary>
    private async Task LoadIntoStagingAsync(DataTable rows, CancellationToken ct)
    {
        IBulkProcessor staging = engines.CreateOracleDirectPathProcessor();
        await staging.SaveAsync(rows, ImportTables.OracleProductsStaging, ct);
    }

    /// <summary>Step 2: conventional path, constraints enforced; the batch moves completely or not at all.</summary>
    private async Task<int> MoveIntoProductsAsync(string batchId, CancellationToken ct)
    {
        try
        {
            return await RunInTransactionAsync(async connection =>
            {
                int moved = await ExecuteAsync(connection, MoveBatchToProducts, batchId, ct);
                await ExecuteAsync(connection, DeleteBatchFromStaging, batchId, ct);
                return moved;
            }, ct);
        }
        catch
        {
            await DiscardBatchAsync(batchId);   // the batch was rolled back into staging; do not leave it there
            throw;
        }
    }

    /// <summary>Removes a batch that could not be moved. A failure is logged (type only) and never hides the original error.</summary>
    private async Task DiscardBatchAsync(string batchId)
    {
        try
        {
            await RunInTransactionAsync(connection => ExecuteAsync(connection, DeleteBatchFromStaging, batchId, CancellationToken.None),
                CancellationToken.None);
        }
        catch (Exception cleanupError)
        {
            logger.LogError("Removing staging batch {BatchId} failed with {ExceptionType}", batchId, cleanupError.GetType().FullName);
        }
    }

    private async Task<int> RunInTransactionAsync(Func<OracleConnection, Task<int>> work, CancellationToken ct)
    {
        await using OracleConnection connection = new(engines.ConnectionStringFor(DatabaseEngine.Oracle));
        await connection.OpenAsync(ct);
        await using OracleTransaction transaction = connection.BeginTransaction();
        try
        {
            int result = await work(connection);   // ODP.NET commands join the connection's transaction
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await RollbackAsync(transaction);
            throw;
        }
    }

    private async Task RollbackAsync(OracleTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackError)
        {
            logger.LogError("Rolling back the staging move failed with {ExceptionType}", rollbackError.GetType().FullName);
        }
    }

    private static async Task<int> ExecuteAsync(OracleConnection connection, string sql, string batchId, CancellationToken ct)
    {
        await using OracleCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.BindByName = true;
        command.Parameters.Add(new OracleParameter("batchId", OracleDbType.Varchar2, batchId, ParameterDirection.Input));
        return await command.ExecuteNonQueryAsync(ct);
    }
}
