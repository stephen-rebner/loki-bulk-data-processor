namespace Loki.BulkDataProcessor.Providers;

/// <summary>Per-call write settings, taken from the processor when the call starts.</summary>
/// <param name="BatchSize">Rows per batch; 0 means the provider's default (usually a single batch).</param>
/// <param name="TimeoutSeconds">Operation timeout in seconds; 0 means no limit.</param>
public sealed record BulkWriteOptions(int BatchSize, int TimeoutSeconds);
