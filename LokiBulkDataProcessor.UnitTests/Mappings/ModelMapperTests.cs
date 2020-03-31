using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.UnitTests.Mappings.TestMappers;
using LokiBulkDataProcessor.UnitTests.TestModels;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Mappings
{
    public class ModelMapperTests
    {
        private TestMapping1 _testMapper;

        [SetUp]
        public void SetUp()
        {
            _testMapper = new TestMapping1();
        }

        [Test]
        public void MapperTest()
        {
            _testMapper.ValidateTheMappings();
            Assert.IsTrue(false);
        }
    }
}
