namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>Identifiers that try to break out of SQL (CWE-89), shared by the contract and the provider-specific tests.</summary>
public static class HostileIdentifiers
{
    /// <summary>A destination table name with a statement separator, a DROP TABLE and a comment.</summary>
    public const string TableName = "TestDbModels; DROP TABLE Blogs--";

    /// <summary>A column name with a quote, a bracket, a statement separator and a comment.</summary>
    public const string ColumnName = "Title\"]; DROP TABLE Blogs--";
}
