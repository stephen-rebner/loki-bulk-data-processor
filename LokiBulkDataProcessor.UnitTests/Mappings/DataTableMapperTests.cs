using FluentAssertions;
using Loki.BulkDataProcessor.Exceptions;
using LokiBulkDataProcessor.UnitTests.Mappings.TestMappers;
using NUnit.Framework;
using System;

namespace LokiBulkDataProcessor.UnitTests.Mappings
{
    public class DataTableMapperTests
    {
        [Test]
        public void ShouldNotThrow_WhenValidatingValidModelMapping()
        {
            Action action = () => new ValidDataTableMapping1();

            action.Should().NotThrow();
        }

        [Test]
        public void ShouldThrow_WhenMappingHasDuplicateDestinationColumn()
        {
            Action action = () => new DataTableMappingWithDuplicateDestCol();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage("The mapping for the DataTableMappingWithDuplicateDestCol data table contains duplicate destination columns.");
        }

        [Test]
        public void ShouldThrow_WhenMappingHasEmptyDestinationColumn()
        {
            Action action = () => new DataTableMappingWithEmptyDestCol();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage($"The mapping for the DataTableMappingWithEmptyDestCol data table contains a null or empty destination column.");
        }

        [Test]
        public void ShouldThrow_WhenMappingHasNullDestinationColumn()
        {
            Action action = () => new DataTableMappingWithNullDestCol();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage($"The mapping for the DataTableMappingWithNullDestCol data table contains a null or empty destination column.");
        }

        [Test]
        public void ShouldThrow_WhenMappingHasDuplicateSourceColumn()
        {
            Action action = () => new DataTableMappingWithDuplicateSourceColumn();

            action.Should().ThrowExactly<MappingException>()
                .WithMessage($"The mapping for the DataTableMappingWithEmptyDestCol data table contains a duplicate source column: PublicString");
        }
    }
}
