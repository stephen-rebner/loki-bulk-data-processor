using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.DataReaders;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Loki.BulkDataProcessor.Pipeline;
using Loki.BulkDataProcessor.Providers;
using Loki.BulkDataProcessor.Sources;
using Loki.BulkDataProcessor.Utils.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loki.BulkDataProcessor.Internal;

/// <summary>
/// The public facade: guards arguments, builds the source, and hands it to the pipeline. Created only by the factory.
/// The connection string is hidden from debugger views and <see cref="ToString"/>.
/// </summary>
[DebuggerDisplay("ProviderName = {ProviderName}")]
internal sealed class BulkProcessor : IBulkProcessor
{
    private readonly IBulkCopyProvider _provider;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string _connectionString;
    private readonly MappingRegistry _mappings;
    private readonly JsonPayloadPolicy _jsonPolicy;
    private readonly BulkCopyPipeline _pipeline;
    private readonly BulkProcessorSettings _settings = new();
    private readonly ILogger<BulkProcessor> _logger;

    public BulkProcessor(IBulkCopyProvider provider, string connectionString, MappingRegistry mappings,
        ILoggerFactory loggerFactory = null, JsonPayloadPolicy jsonPolicy = null)
    {
        loggerFactory ??= NullLoggerFactory.Instance;
        _provider = provider;
        _connectionString = connectionString;
        _mappings = mappings;
        _jsonPolicy = jsonPolicy ?? JsonPayloadPolicy.Default;
        _pipeline = new BulkCopyPipeline(provider, loggerFactory);
        _logger = loggerFactory.CreateLogger<BulkProcessor>();
    }

    public string ProviderName => _provider.Name;

    public int Timeout
    {
        get => _settings.TimeoutSeconds;
        set
        {
            value.ThrowIfLessThanZero(nameof(Timeout));
            _logger.LogInformation("Setting Timeout from {OldValue} to {NewValue}", _settings.TimeoutSeconds, value);
            _settings.SetTimeout(value);
        }
    }

    public int BatchSize
    {
        get => _settings.BatchSize;
        set
        {
            value.ThrowIfLessThanZero(nameof(BatchSize));
            _logger.LogInformation("Setting BatchSize from {OldValue} to {NewValue}", _settings.BatchSize, value);
            _settings.SetBatchSize(value);
        }
    }

    public IDbTransaction Transaction
    {
        get => _settings.ExternalTransaction;
        set
        {
            if (value is not null)
            {
                _provider.AdoptExternalTransaction(value);   // wrong driver → ArgumentException now, before any work
            }

            _logger.LogInformation(value is null ? "Clearing external transaction" : "Setting external transaction");
            _settings.SetTransaction(value);
        }
    }

    public Task SaveAsync<T>(IEnumerable<T> dataToProcess, string destinationTableName, CancellationToken cancellationToken = default) where T : class
    {
        dataToProcess.ThrowIfCollectionIsNullOrEmpty(nameof(dataToProcess));
        destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
        return ExecuteAsync(() => BulkSource.FromModels(dataToProcess, _mappings, _logger), destinationTableName, cancellationToken);
    }

    public Task SaveAsync(DataTable dataTable, string destinationTableName, CancellationToken cancellationToken = default)
    {
        destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
        dataTable.ThrowIfNullOrHasZeroRows();
        return ExecuteAsync(() => BulkSource.FromDataTable(dataTable, destinationTableName, _mappings), destinationTableName, cancellationToken);
    }

    public Task SaveAsync(IDataReader dataReader, string destinationTableName, CancellationToken cancellationToken = default) =>
        SaveAsync(dataReader, destinationTableName, sourceName: null, cancellationToken);

    public Task SaveAsync(IDataReader dataReader, string destinationTableName, string sourceName, CancellationToken cancellationToken = default)
    {
        dataReader.ThrowIfNull(nameof(dataReader));
        destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
        return ExecuteAsync(() => BulkSource.FromDataReader(dataReader, destinationTableName, sourceName, _mappings), destinationTableName, cancellationToken);
    }

    public Task SaveAsync(Stream jsonStream, CancellationToken cancellationToken = default)
    {
        jsonStream.ThrowIfNull(nameof(jsonStream));
        return SaveJsonAsync(jsonStream, destinationTableName: null, cancellationToken);
    }

    public Task SaveAsync(Stream jsonStream, string destinationTableName, CancellationToken cancellationToken = default)
    {
        jsonStream.ThrowIfNull(nameof(jsonStream));
        destinationTableName.ThrowIfNullOrEmptyString(nameof(destinationTableName));
        return SaveJsonAsync(jsonStream, destinationTableName, cancellationToken);
    }

    public override string ToString() => $"BulkProcessor ({ProviderName})";

    /// <summary>A null <paramref name="destinationTableName"/> means the payload's own tableName (legacy overload).</summary>
    private async Task SaveJsonAsync(Stream jsonStream, string destinationTableName, CancellationToken cancellationToken)
    {
        using JsonDocument document = await ParseJsonAsync(jsonStream, cancellationToken).ConfigureAwait(false);
        using JsonDataReader reader = JsonDataReader.Create(document, _logger);   // BulkSource also disposes it; Dispose is idempotent
        string destination = destinationTableName ?? reader.TableName;
        await ExecuteAsync(() => BulkSource.FromJson(reader, destination, _mappings, _jsonPolicy.RequireDataMapping),
            destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonDocument> ParseJsonAsync(Stream jsonStream, CancellationToken cancellationToken)
    {
        if (_jsonPolicy.MaxPayloadBytes is not long maxBytes)
        {
            return await JsonDocument.ParseAsync(jsonStream, JsonPayloadPolicy.DocumentOptions, cancellationToken).ConfigureAwait(false);
        }

        LengthLimitedStream.ThrowIfKnownToExceed(jsonStream, maxBytes);
        await using LengthLimitedStream limited = new(jsonStream, maxBytes);   // does not dispose the caller's stream
        return await JsonDocument.ParseAsync(limited, JsonPayloadPolicy.DocumentOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteAsync(Func<BulkSource> createSource, string destinationTableName, CancellationToken cancellationToken)
    {
        BulkCallSettings settings = _settings.Snapshot(_connectionString);
        string loggedTableName = MessageText.Sanitize(destinationTableName);
        _logger.LogInformation("Starting bulk copy to {TableName} via {Provider}", loggedTableName, _provider.Name);
        try
        {
            using BulkSource source = createSource();
            BulkWriteTarget target = new(destinationTableName, source.Columns);
            await _pipeline.ExecuteAsync(source.Reader, target, settings, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Completed bulk copy to {TableName} via {Provider}", loggedTableName, _provider.Name);
        }
        catch (Exception exception)
        {
            LogFailure(exception, loggedTableName);
            throw;
        }
    }

    /// <summary>
    /// Logs only exception TYPES, never the exception object: driver messages can echo row values (CWE-532), and the
    /// caller still receives the full exception (driver error as InnerException) to log under its own policy.
    /// </summary>
    private void LogFailure(Exception exception, string loggedTableName) =>
        _logger.LogError("Bulk copy to {TableName} via {Provider} failed with {ExceptionType} (inner {InnerExceptionType})",
            loggedTableName, _provider.Name, exception.GetType().FullName, exception.InnerException?.GetType().FullName ?? "none");
}
