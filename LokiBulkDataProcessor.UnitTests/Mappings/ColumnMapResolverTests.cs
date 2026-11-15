using System;
using FluentAssertions;
using Loki.BulkDataProcessor.Mappings.Resolution;
using Loki.BulkDataProcessor.Providers;
using LokiBulkDataProcessor.UnitTests.Mappings.TestMappers;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Mappings;

public class ColumnMapResolverTests
{
    [Test]
    public void Resolve_ShouldMapEverySourceColumnByName_WhenThereIsNoMapping()
    {
        ColumnMapResolver.Resolve(null, new[] { "A", "B" })
            .Should().Equal(new ColumnMap("A", "A"), new ColumnMap("B", "B"));
    }

    [Test]
    public void Resolve_ShouldUseTheMappingsColumnsInMappingOrder_WhenThereIsAMapping()
    {
        ColumnMapResolver.Resolve(new ValidDataMapping2(), new[] { "AnotherColumn", "BaseInt", "Unmapped" })
            .Should().Equal(new ColumnMap("BaseInt", "base_int"), new ColumnMap("AnotherColumn", "another_column"));
    }

    [Test]
    public void ThrowIfSourceColumnMissing_ShouldThrowNamingTheColumn_WhenAMappedSourceColumnIsMissing()
    {
        var columns = new[] { new ColumnMap("A", "a"), new ColumnMap("Missing", "m") };

        Action action = () => ColumnMapResolver.ThrowIfSourceColumnMissing(columns, new[] { "A" }, "dataToProcess");

        action.Should().Throw<ArgumentException>()
            .WithMessage($"{BulkCopyErrorMessages.SourceColumnNotFound("Missing")}*")
            .Which.ParamName.Should().Be("dataToProcess");
    }

    [Test]
    public void ThrowIfSourceColumnMissing_ShouldNotThrow_WhenEveryMappedSourceColumnExists()
    {
        Action action = () => ColumnMapResolver.ThrowIfSourceColumnMissing(new[] { new ColumnMap("A", "a") }, new[] { "A", "B" }, "x");

        action.Should().NotThrow();
    }
}
