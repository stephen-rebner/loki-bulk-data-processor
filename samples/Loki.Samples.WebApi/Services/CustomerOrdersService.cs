using Loki.BulkDataProcessor;
using Loki.Samples.WebApi.Data;
using Loki.Samples.WebApi.Engines;
using Loki.Samples.WebApi.Errors;
using Loki.Samples.WebApi.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Loki.Samples.WebApi.Services;

/// <summary>
/// A caller-owned transaction shared by EF Core and Loki (SQL Server): EF Core inserts the customer, Loki bulk-inserts its orders,
/// and both commit or roll back together. The transaction belongs to this code: Loki never commits, rolls back, disposes or
/// closes it or its connection, also when its save fails (README "Transactions").
/// </summary>
internal sealed class CustomerOrdersService(SamplesDbContext db, EngineCatalog engines, ILogger<CustomerOrdersService> logger)
{
    public async Task<CustomerWithOrdersResult> SaveCustomerWithOrdersAsync(CustomerWithOrdersRequest request, CancellationToken ct)
    {
        Customer customer = ToCustomer(request);
        int orderCount = RequestGuards.ThrowIfNotARowCount(request.OrderCount, "orderCount");

        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await SaveCustomerAsync(customer, ct);                                       // EF Core, inside the transaction
            await SaveOrdersAsync(transaction, OrdersFor(customer.Id, orderCount), ct);  // Loki, inside the same transaction
            ThrowIf(request.SimulateFailure);
            // The caller commits (Loki never does when it was given a transaction). Not cancellable once the work is done:
            // a cancellation here would roll back rows that were all written.
            await transaction.CommitAsync(CancellationToken.None);
            return new CustomerWithOrdersResult(customer.Id, orderCount);
        }
        catch
        {
            await RollbackAsync(transaction);    // Loki left the transaction to us, also after a failed save
            throw;                               // the original error, never the rollback's
        }
    }

    private async Task SaveCustomerAsync(Customer customer, CancellationToken ct)
    {
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);           // EF Core reads the generated Id back
    }

    private async Task SaveOrdersAsync(IDbContextTransaction transaction, List<OrderRow> orders, CancellationToken ct)
    {
        IBulkProcessor processor = engines.CreateProcessor(DatabaseEngine.SqlServer);
        // The Microsoft.Data.SqlClient transaction behind EF Core's. Loki writes on its connection; with a transaction set it
        // opens no connection or transaction of its own and leaves commit, rollback and dispose to us.
        processor.Transaction = transaction.GetDbTransaction();
        await processor.SaveAsync(orders, ImportTables.Orders, ct);
    }

    /// <summary>A rollback failure is logged (its type only) and never replaces the error that caused the rollback.</summary>
    private async Task RollbackAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);   // also when the request was cancelled
        }
        catch (Exception rollbackError)
        {
            logger.LogError("Rolling back the customer and orders failed with {ExceptionType}", rollbackError.GetType().FullName);
        }
    }

    private static Customer ToCustomer(CustomerWithOrdersRequest request) => new()
    {
        Name = RequestGuards.ThrowIfBlankOrLonger(request.Name, 200, "name"),
        Email = RequestGuards.ThrowIfLonger(request.Email, 320, "email"),
        CreatedOn = DateTime.UtcNow
    };

    private static List<OrderRow> OrdersFor(int customerId, int count)
    {
        DateTime now = DateTime.UtcNow;
        return Enumerable.Range(1, count)
            .Select(i => new OrderRow { CustomerId = customerId, Reference = $"ORD-{customerId}-{i:D6}", Amount = 10m + i % 90, OrderedOn = now })
            .ToList();
    }

    private static void ThrowIf(bool simulateFailure)
    {
        if (simulateFailure)
        {
            throw new SimulatedFailureException();
        }
    }
}
