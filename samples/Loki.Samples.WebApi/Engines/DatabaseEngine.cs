namespace Loki.Samples.WebApi.Engines;

/// <summary>
/// The databases this API writes to. A route value is mapped to one of these by <see cref="EngineCatalog"/>, and each one to a
/// Loki provider name and a connection string from configuration: callers never send either.
/// </summary>
internal enum DatabaseEngine
{
    SqlServer,
    Oracle
}
