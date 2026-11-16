using Loki.BulkDataProcessor.Mappings;

namespace LokiBulkDataProcessor.IntegrationTests.Contract.Mappings;

/// <summary>
/// Maps Title to a destination column name that tries to break out of an identifier (quote, bracket, statement separator and
/// comment). Every provider must refuse it without running anything; see the hostile-identifier contract tests.
/// </summary>
public class HostileDestinationDataMapping : DataMapping
{
    public const string SourceName = "HostileDestinationPosts";

    public const string HostileColumn = HostileIdentifiers.ColumnName;

    public override string SourceTableName => SourceName;

    public HostileDestinationDataMapping()
    {
        Map("Title").ToDestinationColumn(HostileColumn);
        Map("Content").ToDestinationColumn("Content");
        Map("BlogId").ToDestinationColumn("BlogId");
    }
}
