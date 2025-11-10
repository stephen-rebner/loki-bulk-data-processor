using FluentAssertions;
using Loki.BulkDataProcessor.Core.Exceptions;
using LokiBulkDataProcessor.Core.UnitTests.Mappings.TestMappers;
using NUnit.Framework;
using System;

namespace LokiBulkDataProcessor.Core.UnitTests.Mappings
{
    public class DataTableMapperTests
    {
        [Test]
        public void ShouldNotThrow_WhenValidatingValidModelMapping()
        {
            Action action = () => new ValidDataMapping1();

            action.Should().NotThrow();
        }

        [Test]
        public void ShouldThrow_WhenMappingHasDuplicateDestinationColumn()
        {
            Action action = () => new DataMappingWithDuplicateDestCol();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage("The mapping for the DataMappingWithDuplicateDestCol data table contains duplicate destination columns.");
        }

        [Test]
        public void ShouldThrow_WhenMappingHasEmptyDestinationColumn()
        {
            Action action = () => new DataMappingWithEmptyDestCol();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage($"The mapping for the DataMappingWithEmptyDestCol data table contains a null or empty destination column.");
        }

        [Test]
        public void ShouldThrow_WhenMappingHasNullDestinationColumn()
        {
            Action action = () => new DataMappingWithNullDestCol();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage($"The mapping for the DataMappingWithNullDestCol data table contains a null or empty destination column.");
        }

        [Test]
        public void ShouldThrow_WhenMappingHasDuplicateSourceColumn()
        {
            Action action = () => new DataMappingWithDuplicateSourceColumn();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage($"The mapping for the DataMappingWithEmptyDestCol data table contains a duplicate source column: PublicString");
        }
    }
}
