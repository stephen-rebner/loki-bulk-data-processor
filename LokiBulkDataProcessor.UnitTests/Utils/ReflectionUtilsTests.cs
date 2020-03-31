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
    }
}