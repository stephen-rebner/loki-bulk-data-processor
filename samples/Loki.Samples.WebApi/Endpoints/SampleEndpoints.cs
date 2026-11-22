using Loki.BulkDataProcessor;
using Loki.Samples.WebApi.Engines;
using Loki.Samples.WebApi.Imports;
using Loki.Samples.WebApi.Services;
using Loki.Samples.WebApi.Staging;
using Microsoft.AspNetCore.Mvc;

namespace Loki.Samples.WebApi.Endpoints;

/// <summary>
/// The HTTP surface. Handlers only parse the route and call a service; failures become problem details in
/// <see cref="Errors.SampleExceptionHandler"/>. See Loki.Samples.WebApi.http for a request per endpoint.
/// </summary>
internal static class SampleEndpoints
{
    public static void MapSampleEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder api = app.MapGroup("/api");
        api.MapGet("/engines", ListEngines);
        MapEngineEndpoints(api.MapGroup("/{engine}"));
        api.MapPost("/sqlserver/customers-with-orders", (CustomerWithOrdersRequest request, CustomerOrdersService service, CancellationToken ct) =>
            service.SaveCustomerWithOrdersAsync(request, ct));
        api.MapPost("/oracle/products/staging-import", (OracleStagingImportService service, CancellationToken ct, int count = 1_000, bool includeDuplicate = false) =>
            service.ImportProductsAsync(count, includeDuplicate, ct));
    }

    private static void MapEngineEndpoints(RouteGroupBuilder engine)
    {
        engine.MapGet("/counts", (string engine, TableCountService service, CancellationToken ct) =>
            service.CountAsync(EngineCatalog.Parse(engine), ct));
        engine.MapPost("/customers", (string engine, IReadOnlyList<CustomerRequest> customers, BulkImportService service, CancellationToken ct) =>
            service.SaveCustomersAsync(EngineCatalog.Parse(engine), customers, ct));
        engine.MapPost("/products/datatable", (string engine, BulkImportService service, CancellationToken ct, int count = 1_000) =>
            service.SaveProductsFromDataTableAsync(EngineCatalog.Parse(engine), count, ct));
        engine.MapPost("/products/reader", (string engine, BulkImportService service, CancellationToken ct, int count = 1_000) =>
            service.SaveProductsFromReaderAsync(EngineCatalog.Parse(engine), count, ct));
        engine.MapPost("/json/{table}", (string engine, string table, HttpRequest request, BulkImportService service, CancellationToken ct) =>
                service.SaveJsonAsync(EngineCatalog.Parse(engine), table, request.Body, ct))
            .Accepts<object>("application/json")
            // Kestrel answers 413 as soon as a body passes the limit; Loki's MaxJsonPayloadBytes is the same limit, enforced
            // again while it reads the stream (for hosts without a request size limit).
            .WithMetadata(new RequestSizeLimitAttribute(JsonUploadLimits.MaxPayloadBytes));
    }

    private static object ListEngines(EngineCatalog engines, IBulkProcessorFactory factory) => new
    {
        Engines = Enum.GetValues<DatabaseEngine>().Select(engine => new { Engine = engine.ToString(), Configured = engines.IsConfigured(engine) }),
        Providers = factory.ProviderNames
    };
}
