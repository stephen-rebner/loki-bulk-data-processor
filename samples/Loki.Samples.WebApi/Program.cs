using Loki.BulkDataProcessor.DependancyInjection;   // (sic) the namespace is spelt this way
using Loki.BulkDataProcessor.Oracle;
using Loki.Samples.WebApi.Data;
using Loki.Samples.WebApi.Endpoints;
using Loki.Samples.WebApi.Engines;
using Loki.Samples.WebApi.Errors;
using Loki.Samples.WebApi.Services;
using Loki.Samples.WebApi.Staging;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Loki: mappings from this assembly, strict JSON options for payloads from callers, and three providers. Loki logs through the
// host's ILoggerFactory (category "Loki.BulkDataProcessor").
builder.Services.AddLokiBulkDataProcessor(options =>
    {
        options.AddMappingsFrom(typeof(Program).Assembly);
        options.RequireDataMappingForJson = true;                   // a JSON payload cannot add columns its mapping does not list
        options.MaxJsonPayloadBytes = JsonUploadLimits.MaxPayloadBytes;
    })
    .AddSqlServer()                                                 // "SqlServer"
    .AddOracle()                                                    // "Oracle": array bind, the default
    .AddOracle(o => o.WriteMode = OracleWriteMode.DirectPath, name: SampleProviders.OracleDirectPath);   // staging tables only

// EF Core (SQL Server): the schema's migrations and the caller-owned transaction sample.
builder.Services.AddDbContext<SamplesDbContext>((services, options) =>
    options.UseSqlServer(services.GetRequiredService<EngineCatalog>().ConnectionStringFor(DatabaseEngine.SqlServer)));

builder.Services.AddSingleton<EngineCatalog>();
builder.Services.AddSingleton<BulkImportService>();
builder.Services.AddSingleton<OracleStagingImportService>();
builder.Services.AddSingleton<TableCountService>();
builder.Services.AddScoped<CustomerOrdersService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SampleExceptionHandler>();

WebApplication app = builder.Build();
app.UseExceptionHandler();
app.MapSampleEndpoints();
await app.ApplyMigrationsIfConfiguredAsync();
await app.RunAsync();
