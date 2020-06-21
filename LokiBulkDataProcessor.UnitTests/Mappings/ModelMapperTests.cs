using FluentAssertions;
using Loki.BulkDataProcessor.Exceptions;
using LokiBulkDataProcessor.UnitTests.Mappings.TestMappers;
using NUnit.Framework;
using System;

namespace LokiBulkDataProcessor.UnitTests.Mappings
{
    public class ModelMapperTests
    {
        [Test]
        public void ShouldNotThrow_WhenValidatingValidModelMapping()
        {
            Action action = () => new ValidModelMapping1();

            action.Should().NotThrow();
        }

        [Test]
        public void ShouldThrow_WhenMappingHasDuplicateDestinationColumn()
        {
            Action action = () => new ModelMappingWithDuplicateDestColumn();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage($"The mapping for the ValidModelObject model contains duplicate destination columns.");
        }

        [Test]
        public void ShouldThrow_WhenMappingHasEmptyDestinationColumn()
        {
            Action action = () => new ModelMappingWithEmptyDestColumn();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage($"The mapping for the ValidModelObject model contains a null or empty destination column.");
        }

        [Test]
        public void ShouldThrow_WhenMappingHasNullDestinationColumn()
        {
            Action action = () => new ModelMappingWithNullDestColumn();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage($"The mapping for the ValidModelObject model contains a null or empty destination column.");
        }

        [Test]
        public void ShouldThrow_WhenMappingHasDuplicateSourceColumn()
        {
            Action action = () => new ModelMappingWithDuplicateSourceColumn();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage($"The mapping contains a duplicate source column: PublicString");
        }
    }
}
