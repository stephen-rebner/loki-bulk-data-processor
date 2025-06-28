using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Loki.BulkDataProcessor.DataReaders;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.DataReaders
{
    [TestFixture]
    public class JsonSchemaValidatorTests
    {
        [Test]
        public void ValidateJsonSchema_ShouldThrow_WhenTableNameIsMissing()
        {
            // Arrange
            var invalidJson = @"{
                ""columns"": [],
                ""data"": []
            }";
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));
            using var document = JsonDocument.Parse(jsonStream);

            // Act
            Action act = () => JsonSchemaValidator.ValidateJsonSchema(document);

            // Assert
            act.Should().Throw<InvalidDataException>().WithMessage("Missing or invalid 'tableName' property.");
        }

        [Test]
        public void ValidateJsonSchema_ShouldThrow_WhenColumnsArrayIsMissing()
        {
            // Arrange
            var invalidJson = @"{
                ""tableName"": ""TestTable"",
                ""data"": []
            }";
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));
            using var document = JsonDocument.Parse(jsonStream);

            // Act
            Action act = () => JsonSchemaValidator.ValidateJsonSchema(document);

            // Assert
            act.Should().Throw<InvalidDataException>().WithMessage("Missing or empty 'columns' array.");
        }

        [Test]
        public void ValidateJsonSchema_ShouldThrow_WhenColumnNameOrTypeIsMissing()
        {
            // Arrange
            var invalidJson = @"{
                ""tableName"": ""TestTable"",
                ""columns"": [
                    { ""name"": ""Id"" }
                ],
                ""data"": []
            }";
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));
            using var document = JsonDocument.Parse(jsonStream);

            // Act
            Action act = () => JsonSchemaValidator.ValidateJsonSchema(document);

            // Assert
            act.Should().Throw<InvalidDataException>().WithMessage("Column missing valid 'type' property.");
        }

        [Test]
        public void ValidateJsonSchema_ShouldThrow_WhenDataArrayIsMissing()
        {
            // Arrange
            var invalidJson = @"{
                ""tableName"": ""TestTable"",
                ""columns"": [
                    { ""name"": ""Id"", ""type"": ""int"" }
                ]
            }";
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));
            using var document = JsonDocument.Parse(jsonStream);

            // Act
            Action act = () => JsonSchemaValidator.ValidateJsonSchema(document);

            // Assert
            act.Should().Throw<InvalidDataException>().WithMessage("Missing or empty 'data' array.");
        }

        [Test]
        public void ValidateJsonSchema_ShouldNotThrow_WhenJsonIsValid()
        {
            // Arrange
            var validJson = @"{
                ""tableName"": ""TestTable"",
                ""columns"": [
                    { ""name"": ""Id"", ""type"": ""int"" },
                    { ""name"": ""Name"", ""type"": ""string"" }
                ],
                ""data"": [
                    { ""Id"": 1, ""Name"": ""Test"" }
                ]
            }";
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(validJson));
            using var document = JsonDocument.Parse(jsonStream);

            // Act
            Action act = () => JsonSchemaValidator.ValidateJsonSchema(document);

            // Assert
            act.Should().NotThrow();
        }
        
        [Test]
        public void ValidateJsonSchema_ShouldThrow_WhenJsonIsEmpty()
        {
            // Arrange
            var emptyJson = @"{}";
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(emptyJson));
            using var document = JsonDocument.Parse(jsonStream);
        
            // Act
            Action act = () => JsonSchemaValidator.ValidateJsonSchema(document);
        
            // Assert
            act.Should().Throw<InvalidDataException>().WithMessage("Missing or invalid 'tableName' property.");
        }
        
        [Test]
        public void ValidateJsonSchema_ShouldThrow_WhenColumnsArrayContainsEmptyObjects()
        {
            // Arrange
            var invalidJson = @"{
                ""tableName"": ""TestTable"",
                ""columns"": [{}],
                ""data"": []
            }";
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));
            using var document = JsonDocument.Parse(jsonStream);
        
            // Act
            Action act = () => JsonSchemaValidator.ValidateJsonSchema(document);
        
            // Assert
            act.Should().Throw<InvalidDataException>().WithMessage("Column missing valid 'name' property.");
        }
        
        [Test]
        public void ValidateJsonSchema_ShouldThrow_WhenColumnsArrayContainsDuplicateNames()
        {
            // Arrange
            var invalidJson = @"{
                ""tableName"": ""TestTable"",
                ""columns"": [
                    { ""name"": ""Id"", ""type"": ""int"" },
                    { ""name"": ""Id"", ""type"": ""string"" }
                ],
                ""data"": []
            }";
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));
            using var document = JsonDocument.Parse(jsonStream);
        
            // Act
            Action act = () => JsonSchemaValidator.ValidateJsonSchema(document);
        
            // Assert
            act.Should().Throw<InvalidDataException>().WithMessage("Duplicate column name 'Id' found in the schema.");
        }
        
        [Test]
        public void ValidateJsonSchema_ShouldNotThrow_WhenDataArrayContainsExtraProperties()
        {
            // Arrange
            var validJson = @"{
                ""tableName"": ""TestTable"",
                ""columns"": [
                    { ""name"": ""Id"", ""type"": ""int"" }
                ],
                ""data"": [
                    { ""Id"": 1, ""ExtraProperty"": ""ExtraValue"" }
                ]
            }";
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(validJson));
            using var document = JsonDocument.Parse(jsonStream);
        
            // Act
            Action act = () => JsonSchemaValidator.ValidateJsonSchema(document);
        
            // Assert
            act.Should().NotThrow();
        }
        
        [Test]
        public void ValidateJsonSchema_ShouldThrow_WhenColumnTypeIsInvalid()
        {
            // Arrange
            var invalidJson = @"{
                ""tableName"": ""TestTable"",
                ""columns"": [
                    { ""name"": ""Id"", ""type"": ""unsupportedType"" }
                ],
                ""data"": []
            }";
            using var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));
            using var document = JsonDocument.Parse(jsonStream);
        
            // Act
            Action act = () => JsonSchemaValidator.ValidateJsonSchema(document);
        
            // Assert
            act.Should().Throw<InvalidDataException>().WithMessage("Unsupported column type 'unsupportedType' for column 'Id'.");
        }
    }
}