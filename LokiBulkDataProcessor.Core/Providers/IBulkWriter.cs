using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Providers;

/// <summary>Writes one source into one destination table inside the context's transaction. Loki disposes it after each write.</summary>
public interface IBulkWriter : IAsyncDisposable
{
    /// <summary>Copies every row of <paramref name="source"/> into <paramref name="target"/> using its column maps.</summary>
    /// <remarks>Implementations may complete synchronously when the driver has no asynchronous API;
    /// they must then check <paramref name="cancellationToken"/> before the load starts.</remarks>
    Task WriteAsync(IDataReader source, BulkWriteTarget target, CancellationToken cancellationToken);
}
