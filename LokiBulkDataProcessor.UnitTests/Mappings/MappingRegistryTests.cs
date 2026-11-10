using System;
using System.Reflection;
using FluentAssertions;
using Loki.BulkDataProcessor.Exceptions;
using Loki.BulkDataProcessor.Mappings.InternalMapperStorage;
using LokiBulkDataProcessor.UnitTests.Mappings.TestMappers;
using LokiBulkDataProcessor.UnitTests.TestDoubles;
using LokiBulkDataProcessor.UnitTests.TestModels;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Mappings;

public class MappingRegistryTests
{
    [Test]
    public void Registry_ShouldFindModelAndDataMappingsByKey_WhenTheyAreRegistered()
    {
        MappingRegistry registry = TestMappingRegistry.Valid();

        registry.Models.GetMappingFor(typeof(ValidModelObject)).Should().BeOfType<ValidModelMapping1>();
        registry.Models.GetMappingFor(typeof(ValidModelObjectB)).Should().BeNull();
        registry.Data.GetMappingFor("TableB").Should().BeOfType<ValidDataMapping2>();
        registry.Data.GetMappingFor("tableb").Should().BeNull("SourceTableName matching is ordinal");
        registry.Data.GetMappingFor(null).Should().BeNull();
    }

    [Test]
    public void ModelMappings_ShouldThrowInvalidOperationException_WhenTwoMappingsTargetTheSameModel()
    {
        Action action = () => new ModelMappings(new[] { typeof(ValidModelMapping1), typeof(SecondValidModelObjectMapping) });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"More than one mapping is registered for the model type '{typeof(ValidModelObject)}'*{nameof(ValidModelMapping1)}*{nameof(SecondValidModelObjectMapping)}*");
    }

    [Test]
    public void DataMappings_ShouldThrowInvalidOperationException_WhenTwoMappingsShareASourceTableName()
    {
        Action action = () => new DataMappings(new[] { typeof(ValidDataMapping2), typeof(SecondTableBDataMapping) });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("More than one mapping is registered for the source table name 'TableB'*");
    }

    [Test]
    public void Mappings_ShouldThrowInvalidOperationExceptionKeepingTheCause_WhenAMappingFailsToInstantiate()
    {
        Action action = () => new DataMappings(new[] { typeof(DataMappingWithDuplicateDestCol) });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("There was an issue instantiating the the mappings. The mapping for the DataMappingWithDuplicateDestCol data table contains duplicate destination columns.")
            .WithInnerException<MappingException>();
    }

    [Test]
    public void Registry_ShouldHaveNoMappings_WhenNoAssembliesAreGiven()
    {
        var registry = new MappingRegistry(Array.Empty<Assembly>());

        registry.Models.GetMappingFor(typeof(ValidModelObject)).Should().BeNull();
        registry.Data.GetMappingFor("TableB").Should().BeNull();
    }

    [Test]
    public void Registry_ShouldScanAnAssemblyOnce_WhenItIsAddedTwice()
    {
        Assembly assemblyWithNoMappings = typeof(MappingRegistry).Assembly;

        Action action = () => new MappingRegistry(new[] { assemblyWithNoMappings, assemblyWithNoMappings, null });

        action.Should().NotThrow();
    }
}
