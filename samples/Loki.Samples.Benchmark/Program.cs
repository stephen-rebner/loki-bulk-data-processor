using Loki.BulkDataProcessor;
using Loki.BulkDataProcessor.DependancyInjection;   // (sic) the namespace is spelt this way
using Loki.Samples.Benchmark;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Loki vs EF Core AddRange + SaveChanges. Usage: dotnet run -c Release -- --rows 10000 --engine all --runs 3
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddCommandLine(args, BenchmarkOptions.SwitchMappings);
builder.Services.AddLokiBulkDataProcessor(options => options.AddMappingsFrom(typeof(BenchmarkRow).Assembly))
    .AddSqlServer()
    .AddOracle();   // array bind, the default write mode

using IHost host = builder.Build();
IConfiguration configuration = host.Services.GetRequiredService<IConfiguration>();
try
{
    BenchmarkOptions options = BenchmarkOptions.From(configuration);
    IReadOnlyList<BenchmarkTarget> targets = BenchmarkTargets.From(configuration, options);
    BenchmarkRunner runner = new([new LokiInsert(host.Services.GetRequiredService<IBulkProcessorFactory>()), new EfCoreInsert()], Console.Out);

    Console.WriteLine($"Inserting {options.Rows:N0} rows, {options.Runs} timed run(s) per method after a warm-up, on {string.Join(", ", targets.Select(t => t.Engine))}.");
    List<MethodResult> results = [];
    foreach (BenchmarkTarget target in targets)
    {
        results.AddRange(await runner.RunAsync(target, options.Rows, options.Runs, CancellationToken.None));
    }

    ResultTable.Write(Console.Out, results);
    return 0;
}
catch (Exception e) when (e is ArgumentException or InvalidOperationException)
{
    // Loki's and this sample's messages are data-free; the driver's error (if any) is only in the InnerException.
    Console.Error.WriteLine($"{e.GetType().Name}: {e.Message}");
    return 1;
}
