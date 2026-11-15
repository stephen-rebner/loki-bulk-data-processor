using System;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using LokiBulkDataProcessor.UnitTests.Mappings.TestMappers;

namespace LokiBulkDataProcessor.UnitTests.TestDoubles;

/// <summary>Registries built from explicit mapping types: the test assembly also holds deliberately invalid mappings.</summary>
internal static class TestMappingRegistry
{
    /// <summary>ValidModelMapping1 (ValidModelObject), ComputedPropertyModelMapping, ValidDataMapping1 ("ValidDataMapping1") and ValidDataMapping2 ("TableB").</summary>
    public static MappingRegistry Valid() => Create(
        new[] { typeof(ValidModelMapping1), typeof(ComputedPropertyModelMapping) },
        new[] { typeof(ValidDataMapping1), typeof(ValidDataMapping2) });

    public static MappingRegistry Create(Type[] modelMappingTypes, Type[] dataMappingTypes) =>
        new(new ModelMappings(modelMappingTypes), new DataMappings(dataMappingTypes));
}
