using System;
using System.Text.Json;
using FluentAssertions;
using Loki.BulkDataProcessor.DataReaders;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using Loki.BulkDataProcessor.Sources;
using LokiBulkDataProcessor.UnitTests.TestDoubles;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Sources;

public class BulkSourceTests
{
    private const string Payload = """
        { "tableName": "Unmapped", "columns": [ { "name": "BaseInt", "type": "int" } ], "data": [ { "BaseInt": 1 } ] }
        """;

    [Test]
    public void FromJson_ShouldDisposeTheReader_WhenBuildingTheSourceThrows()
    {
        using JsonDocument document = JsonDocument.Parse(Payload);
        JsonDataReader reader = JsonDataReader.Create(document);

        Action action = () => BulkSource.FromJson(reader, "Unmapped", MappingRegistry.Empty, requireMapping: true);

        action.Should().Throw<InvalidOperationException>().WithMessage("*'Unmapped'*RequireDataMappingForJson*");
        reader.IsClosed.Should().BeTrue();
    }

    [Test]
    public void FromJson_ShouldKeepTheReaderOpenUntilTheSourceIsDisposed_WhenBuildingTheSourceSucceeds()
    {
        using JsonDocument document = JsonDocument.Parse(Payload);
        JsonDataReader reader = JsonDataReader.Create(document);

        BulkSource source = BulkSource.FromJson(reader, "TableB", TestMappingRegistry.Valid(), requireMapping: true);

        reader.IsClosed.Should().BeFalse();
        source.Dispose();
        reader.IsClosed.Should().BeTrue();
    }

    [Test]
    public void FromJson_ShouldSanitiseTheDestinationInTheMissingMappingMessage_WhenItContainsControlCharacters()
    {
        using JsonDocument document = JsonDocument.Parse(Payload);

        Action action = () => BulkSource.FromJson(JsonDataReader.Create(document), "Bad\r\nName", MappingRegistry.Empty, requireMapping: true);

        action.Should().Throw<InvalidOperationException>().WithMessage("*'Bad??Name'*");
    }
}
