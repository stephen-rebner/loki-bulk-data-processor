using FluentAssertions;
using Loki.BulkDataProcessor.Utils.Reflection;
using NUnit.Framework;
using System;
using LokiBulkDataProcessor.UnitTests.TestModels;
using System.Reflection;
using System.Linq;

namespace LokiBulkDataProcessor.UnitTests.Utils
{
    public class ReflectionUtilsTests
    {
        [Test]
        public void GetPublicPropertyNames_ReturnsOnlyPublicProperties()
        {
            string[] expected = new string[]
            {
                nameof(ValidModelObject.PublicInt),
                nameof(ValidModelObject.PublicString),
                nameof(ValidModelObject.PublicBool),
                nameof(ValidModelObject.BaseInt)
            };

            var type = typeof(ValidModelObject);

            var actual = type.GetPublicPropertyNames();

            expected.Should().BeEquivalentTo(actual);
        }

        [Test]
        public void GetPublicPropertyNames_ShouldThrowWhenNoPublicPropsExist()
        {
            var type = typeof(InvalidModelObject);
            Action action = () => type.GetPublicPropertyNames();

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The data model you passed contains no public properties (Parameter 'publicProperties')");
        }

        [Test]
        public void GetPublicPropertyNames_ShouldSkipGetOnlyProperties()
        {
            var actual = typeof(ModelWithGetOnlyProperties).GetPublicPropertyNames();

            actual.Should().BeEquivalentTo(nameof(ModelWithGetOnlyProperties.Id));
        }

        [Test]
        public void GetPublicPropertyNames_ShouldSkipSetOnlyProperties()
        {
            var actual = typeof(ModelWithSetOnlyProperty).GetPublicPropertyNames();

            actual.Should().BeEquivalentTo(nameof(ModelWithSetOnlyProperty.Id));
        }

        [Test]
        public void GetPublicPropertyNames_ShouldThrow_WhenAllPropertiesAreGetOnly()
        {
            Action action = () => typeof(ModelWithOnlyGetOnlyProperties).GetPublicPropertyNames();

            action.Should()
              .Throw<ArgumentException>()
              .WithMessage("The data model you passed contains no public properties (Parameter 'publicProperties')");
        }

        [Test]
        public void FindTypesDerivedFrom_ShouldReturnModelObject_DerivedFromAbstactModel()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();

            var expected = new [] { typeof(ValidModelObject).FullName, typeof(ValidModelObjectB).FullName };

            var result = executingAssembly.FindTypesDerivedFrom(typeof(AbstractModelObject));

            var resultName = result.Select(x => x.FullName);

            expected.Should().BeEquivalentTo(resultName);
        }

        [Test]
        public void FindTypesDerivedFrom_ShouldReturnEmptyResult_WhenNoObjectsDeriveFromType()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();

            var result = executingAssembly.FindTypesDerivedFrom(typeof(ValidModelObject));

            result.Should().BeEmpty();
        }

        private class ModelWithGetOnlyProperties
        {
            public int Id { get; set; }
            public string Reference => $"ORD-{Id}";
            public string Created { get; } = "now";
        }

        private class ModelWithSetOnlyProperty
        {
            private string _secret;
            public int Id { get; set; }
            public string Secret { set => _secret = value; }
        }

        private class ModelWithOnlyGetOnlyProperties
        {
            public int Id => 1;
        }
    }
}