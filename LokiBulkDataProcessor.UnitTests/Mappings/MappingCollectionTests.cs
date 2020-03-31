using FluentAssertions;
using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.UnitTests.Mappings.TestMappers;
using LokiBulkDataProcessor.UnitTests.TestModels;
using NUnit.Framework;
using System.Reflection;

namespace LokiBulkDataProcessor.UnitTests.Mappings
{
    public class MappingCollectionTests
    {
        private MappingCollection _mappingCollection;

        [SetUp]
        public void SetUp()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();

            _mappingCollection = new MappingCollection(executingAssembly);
        }

        [Test]
        public void GetMapping_ShouldReturnMapping_OfTypeTestMapper1()
        {
            var result = _mappingCollection.GetMappingFor(typeof(ValidModelObject));

            result.Should().BeOfType<TestMapping1>();
        }

        [Test]
        public void GetMapping_ShouldReturnMapping_OfTypeTestMapper2()
        {
            var result = _mappingCollection.GetMappingFor(typeof(ValidModelObjectB));

            result.Should().BeOfType<TestMapping2>();
        }
    }
}
