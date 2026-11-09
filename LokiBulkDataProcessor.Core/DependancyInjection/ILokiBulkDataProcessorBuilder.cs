using Microsoft.Extensions.DependencyInjection;

namespace Loki.BulkDataProcessor.DependancyInjection;

/// <summary>Returned by <c>AddLokiBulkDataProcessor</c>; provider packages extend it (for example <c>.AddSqlServer()</c>).</summary>
public interface ILokiBulkDataProcessorBuilder
{
    /// <summary>The service collection Loki is being registered in.</summary>
    IServiceCollection Services { get; }
}
