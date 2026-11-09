using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Internal;
using Loki.BulkDataProcessor.Providers;
using Microsoft.Extensions.Logging;

namespace Loki.BulkDataProcessor.Pipeline;

/// <summary>
/// The one write flow every input shape and provider goes through: open or join → write → commit (own transaction
/// only) → on failure roll back (own transaction only) and rethrow → dispose what Loki created.
/// </summary>
internal sealed class BulkCopyPipeline(IBulkCopyProvider provider, ILoggerFactory loggerFactory)
{
    private readonly ILogger<BulkCopyPipeline> _logger = loggerFactory.CreateLogger<BulkCopyPipeline>();

    public async Task ExecuteAsync(IDataReader source, BulkWriteTarget target, BulkCallSettings settings, CancellationToken cancellationToken)
    {
        await using ConnectionScope scope = await ConnectionScope
            .OpenAsync(provider, settings, _logger, cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAsync(scope, source, target, settings, cancellationToken).ConfigureAwait(false);
            await scope.CompleteAsync().ConfigureAwait(false);
        }
        catch
        {
            await scope.RollbackAfterFailureAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task WriteAsync(ConnectionScope scope, IDataReader source, BulkWriteTarget target,
        BulkCallSettings settings, CancellationToken cancellationToken)
    {
        BulkWriteContext context = new(scope.Connection, scope.Transaction,
            new BulkWriteOptions(settings.BatchSize, settings.TimeoutSeconds), loggerFactory);
        await using IBulkWriter writer = provider.CreateWriter(context);
        try
        {
            await writer.WriteAsync(source, target, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            ThrowTranslated(exception, target);
            throw;
        }
    }

    /// <summary>Throws the provider's translation, guaranteeing it carries the original as InnerException (#15).
    /// Returns (so the caller rethrows the original) when the provider returned null or the same exception.</summary>
    private void ThrowTranslated(Exception exception, BulkWriteTarget target)
    {
        Exception translated = provider.TranslateException(exception, target);
        if (translated is null || ReferenceEquals(translated, exception))
        {
            return;
        }

        if (!HasInner(translated, exception))
        {
            translated = new InvalidOperationException(translated.Message, exception);
        }

        throw translated;
    }

    private static bool HasInner(Exception translated, Exception original)
    {
        for (Exception inner = translated.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (ReferenceEquals(inner, original))
            {
                return true;
            }
        }

        return false;
    }
}
