namespace Loki.Samples.WebApi.Engines;

/// <summary>Provider names this sample registers in addition to <c>SqlServerProvider.Name</c> and <c>OracleProvider.Name</c>.</summary>
internal static class SampleProviders
{
    /// <summary>
    /// The Oracle provider in <c>OracleWriteMode.DirectPath</c>: STAGING TABLES ONLY (see the README section "Oracle direct path").
    /// Only <see cref="Staging.OracleStagingImportService"/> uses it, with a table name fixed in code.
    /// </summary>
    public const string OracleDirectPath = "Oracle.DirectPath";
}
