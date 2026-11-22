using Microsoft.Extensions.Configuration;

namespace Loki.Samples.Benchmark;

/// <summary>What to run: from appsettings.json "Benchmark", overridden by --rows, --engine and --runs.</summary>
internal sealed record BenchmarkOptions(int Rows, IReadOnlyList<string> Engines, int Runs)
{
    public const string SqlServer = "sqlserver";
    public const string Oracle = "oracle";

    /// <summary>Command-line switches, mapped onto the "Benchmark" configuration section.</summary>
    public static readonly Dictionary<string, string> SwitchMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["--rows"] = "Benchmark:Rows",
        ["--engine"] = "Benchmark:Engine",
        ["--runs"] = "Benchmark:Runs"
    };

    public static BenchmarkOptions From(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection("Benchmark");
        int rows = InRange(section.GetValue("Rows", 10_000), 1, 10_000_000, "--rows");
        int runs = InRange(section.GetValue("Runs", 3), 1, 20, "--runs");
        return new BenchmarkOptions(rows, EnginesFrom(section["Engine"] ?? "all"), runs);
    }

    private static IReadOnlyList<string> EnginesFrom(string engine) => engine.ToLowerInvariant() switch
    {
        "all" => [SqlServer, Oracle],
        SqlServer => [SqlServer],
        Oracle => [Oracle],
        _ => throw new ArgumentException("--engine must be sqlserver, oracle or all.")
    };

    private static int InRange(int value, int min, int max, string name) =>
        value >= min && value <= max ? value : throw new ArgumentException($"{name} must be between {min} and {max}.");
}
