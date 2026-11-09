using System;
using System.Collections.Generic;
using System.Data;
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

/// <summary>The public facade: guards arguments, builds the source, and hands it to the pipeline. Created only by the factory.</summary>
internal sealed class BulkProcessor : IBulkProcessor
{
    private readonly IBulkCopyProvider _provider;
    private readonly string _connectionString;
    private readonly MappingRegistry _mappings;
    private readonly BulkCopyPipeline _pipeline;
    private readonly BulkProcessorSettings _settings = new();
    private readonly ILogger<BulkProcessor> _logger;

    public BulkProcessor(IBulkCopyProvider provider, string connectionString, MappingRegistry mappings, ILoggerFactory loggerFactory = null)
    {
        loggerFactory ??= NullLoggerFactory.Instance;
        _provider = provider;
        _connectionString = connectionString;
        _mappings = mappings;
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

    public async Task SaveAsync(Stream jsonStream, CancellationToken cancellationToken = default)
    {
        jsonStream.ThrowIfNull(nameof(jsonStream));
        using JsonDocument document = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonDataReader reader = JsonDataReader.Create(document, _logger);
        await ExecuteAsync(() => BulkSource.FromJson(reader, _mappings), reader.TableName, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteAsync(Func<BulkSource> createSource, string destinationTableName, CancellationToken cancellationToken)
    {
        BulkCallSettings settings = _settings.Snapshot(_connectionString);
        _logger.LogInformation("Starting bulk copy to {TableName} via {Provider}", destinationTableName, _provider.Name);
        try
        {
            using BulkSource source = createSource();
            BulkWriteTarget target = new(destinationTableName, source.Columns);
            await _pipeline.ExecuteAsync(source.Reader, target, settings, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Completed bulk copy to {TableName} via {Provider}", destinationTableName, _provider.Name);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Bulk copy to {TableName} via {Provider} failed", destinationTableName, _provider.Name);
            throw;
        }
    }
}
