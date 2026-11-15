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
        Exception translated = TranslateOrWrap(exception, target);
        if (translated is null || ReferenceEquals(translated, exception))
        {
            return;
        }

        throw HasInner(translated, exception) ? translated : WrapWithFixedMessage(exception, target);
    }

    /// <summary>
    /// Asks the provider to translate the error. If the translator itself throws, its failure is logged (type only) and the
    /// original is wrapped with the fixed, data-free message instead, so the driver error is never lost (#15).
    /// </summary>
    private Exception TranslateOrWrap(Exception original, BulkWriteTarget target)
    {
        try
        {
            return provider.TranslateException(original, target);
        }
        catch (Exception translatorException)
        {
            _logger.LogError("Translating the bulk copy error failed with {ExceptionType}; the original is rethrown as InnerException",
                translatorException.GetType().FullName);
            return WrapWithFixedMessage(original, target);
        }
    }

    /// <summary>The translation lost the original, so it is replaced by a fixed, data-free message that keeps the
    /// original as InnerException. The translation's own message is not reused: it may echo driver text or row data.</summary>
    private static InvalidOperationException WrapWithFixedMessage(Exception original, BulkWriteTarget target) =>
        new(BulkCopyErrorMessages.BulkCopyFailed(target.TableName), original);

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
