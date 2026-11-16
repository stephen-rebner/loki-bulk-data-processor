using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>
/// Runs a save while another session holds a table lock for <see cref="LockHeldFor"/>, then releases it by rolling back. Shared by the
/// contract's timeout tests and the provider-specific ones, so every provider uses the same timing.
/// </summary>
/// <remarks>
/// The lock is released on purpose rather than held until the save fails: a driver delivers a timeout or a cancellation to the server as
/// a "break", and a server without out-of-band breaks (for example Oracle with <c>DISABLE_OOB=ON</c>, as in the gvenzl/oracle-free
/// images) acts on it only when the current call returns. The tests use a 1 s Timeout or cancel 1 s after the save starts, so the break
/// is always sent well before the release; <see cref="LockHeldFor"/> also leaves room for Loki to open its connection and reach the
/// INSERT on a slow CI runner before the lock goes away.
/// </remarks>
public static class HeldTableLock
{
    public static readonly TimeSpan LockHeldFor = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Runs <paramref name="save"/> while <paramref name="lockHolder"/> (whose lock is already taken) is held for <see cref="LockHeldFor"/>.
    /// A failure of <paramref name="save"/> always surfaces, also when releasing the lock fails as well.
    /// </summary>
    public static async Task WhileHeldAsync(DbTransaction lockHolder, Func<Task> save)
    {
        Task release = ReleaseAfterAsync(lockHolder, LockHeldFor);
        try
        {
            await save();
        }
        catch
        {
            await Task.WhenAny(release);   // wait for the release without letting its own failure replace the save's
            throw;
        }

        await release;
    }

    private static async Task ReleaseAfterAsync(DbTransaction lockHolder, TimeSpan delay)
    {
        await Task.Delay(delay);
        await lockHolder.RollbackAsync();
    }
}
