using FluentAssertions;
using Loki.BulkDataProcessor.Utils.Reflection;
using NUnit.Framework;
using System;
using LokiBulkDataProcessor.UnitTests.TestModels;

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
                nameof(ValidModelObject.PublicBool)
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
    }
}