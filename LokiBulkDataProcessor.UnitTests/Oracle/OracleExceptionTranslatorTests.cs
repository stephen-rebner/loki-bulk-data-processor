using System;
using System.Reflection;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle;
using Loki.BulkDataProcessor.Oracle.Internal;
using Loki.BulkDataProcessor.Providers;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public class OracleExceptionTranslatorTests
{
    private const string RowValue = "secret-row-value";

    private static readonly BulkWriteTarget Target = new("NonExistentTable", Array.Empty<ColumnMap>());

    /// <summary>OracleException has no public constructor; this uses ODP.NET's internal (number, dataSource, procedure,
    /// message, ...) one. If a driver update removes it the test is inconclusive and <c>FromNumber</c> still covers the mapping.</summary>
    private static OracleException NewOracleException(int number, string message)
    {
        ConstructorInfo ctor = typeof(OracleException).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(int), typeof(string), typeof(string), typeof(string), typeof(int) }, null);
        if (ctor is null)
        {
            Assert.Inconclusive("ODP.NET no longer exposes the internal OracleException constructor this test uses.");
        }

        return (OracleException)ctor.Invoke(new object[] { number, "ds", "proc", message, 0 });
    }

    [Test]
    public void Translate_ShouldEchoTheCallersTableNameAndTheOraCode_WhenTheTableDoesNotExist()
    {
        OracleException original = NewOracleException(942, "ORA-00942: table or view does not exist");

        Exception translated = OracleExceptionTranslator.Translate(original, Target);

        translated.Should().BeOfType<InvalidOperationException>().Which.Message.Should()
            .StartWith("Bulk copy to 'NonExistentTable' failed (Oracle error ORA-00942). See InnerException for details.")
            .And.Contain("OracleIdentifierResolution.CatalogLookup");
        translated.InnerException.Should().BeSameAs(original);
    }

    [TestCase(904)]
    [TestCase(2373)]
    public void Translate_ShouldReturnTheColumnMappingMismatchMessage_WhenAColumnDoesNotExist(int number)
    {
        OracleException original = NewOracleException(number, "ORA-00904: \"NOPE\": invalid identifier");

        Exception translated = OracleExceptionTranslator.Translate(original, Target);

        translated.Message.Should().StartWith(BulkCopyErrorMessages.ColumnMappingMismatch)
            .And.Contain(OracleExceptionTranslator.ErrorCode(number)).And.NotContain("NOPE");
        translated.InnerException.Should().BeSameAs(original);
    }

    [Test]
    public void Translate_ShouldNotEchoTheRowValue_WhenTheOracleMessageContainsIt()
    {
        OracleException original = NewOracleException(12899, $"ORA-12899: value too large for column \"POSTS\".\"TITLE\" (actual: 300, maximum: 200) '{RowValue}'");

        Exception translated = OracleExceptionTranslator.Translate(original, new BulkWriteTarget("Posts", Array.Empty<ColumnMap>()));

        translated.Message.Should().Be("Bulk copy to 'Posts' failed (Oracle error ORA-12899). See InnerException for details.");
        translated.Message.Should().NotContain(RowValue);
        translated.InnerException.Should().BeSameAs(original);
    }

    [Test]
    public void Translate_ShouldFindTheOracleExceptionInTheInnerChain_WhenTheDriverWrapsIt()
    {
        OracleException oracle = NewOracleException(1, "ORA-00001: unique constraint violated");
        var original = new InvalidOperationException("wrapper", oracle);

        Exception translated = OracleExceptionTranslator.Translate(original, new BulkWriteTarget("Posts", Array.Empty<ColumnMap>()));

        translated.Message.Should().Contain("(Oracle error ORA-00001)");
        translated.InnerException.Should().BeSameAs(original);
    }

    [TestCase(942, "Bulk copy to 'T' failed (Oracle error ORA-00942). See InnerException for details. The table does not exist*")]
    [TestCase(39826, "Bulk copy to 'T' failed (Oracle error ORA-39826). See InnerException for details. The table does not exist*")]
    [TestCase(904, BulkCopyErrorMessages.ColumnMappingMismatch + " (Oracle error ORA-00904) Unquoted Oracle names*")]
    [TestCase(2373, BulkCopyErrorMessages.ColumnMappingMismatch + " (Oracle error ORA-02373) Unquoted Oracle names*")]
    [TestCase(2291, "Bulk copy to 'T' failed (Oracle error ORA-02291). See InnerException for details.")]
    [TestCase(1, "Bulk copy to 'T' failed (Oracle error ORA-00001). See InnerException for details.")]
    [TestCase(1502, "Bulk copy to 'T' failed (Oracle error ORA-01502). See InnerException for details.")]
    public void FromNumber_ShouldMapEachOraCodeToAFixedMessage_WhenGivenAnErrorNumber(int number, string expectedPattern)
    {
        var inner = new Exception($"driver text {RowValue}");

        Exception translated = OracleExceptionTranslator.FromNumber(number, inner, "T");

        translated.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Match(expectedPattern).And.NotContain(RowValue);
        translated.InnerException.Should().BeSameAs(inner);
    }

    [Test]
    public void Translate_ShouldUseTheGenericFixedMessage_WhenTheErrorIsNotAnOracleException()
    {
        var original = new FormatException($"bad value {RowValue}");

        Exception translated = OracleExceptionTranslator.Translate(original, new BulkWriteTarget("Posts", Array.Empty<ColumnMap>()));

        translated.Message.Should().Be("Bulk copy to 'Posts' failed. See InnerException for details.");
        translated.InnerException.Should().BeSameAs(original);
    }

    [Test]
    public void Translate_ShouldSanitiseTheTableName_WhenItContainsControlCharacters()
    {
        Exception translated = OracleExceptionTranslator.FromNumber(1, new Exception(), "Posts\r\nFORGED");

        translated.Message.Should().StartWith("Bulk copy to 'Posts??FORGED' failed");
    }

    [Test]
    public void Translate_ShouldReturnTheSameException_WhenItIsTheProvidersOwnValidationFailure()
    {
        ArgumentException validation = OracleValidation.Argument("The table 'x' is not valid.", "TableName");

        OracleExceptionTranslator.Translate(validation, Target).Should().BeSameAs(validation);
    }

    [Test]
    public void Translate_ShouldWrap_WhenAnExceptionForgesTheOldValidationMarkerInItsData()
    {
        var forged = new InvalidOperationException($"reader error {RowValue}");
        forged.Data["Loki.BulkDataProcessor.Oracle.ValidationFailure"] = true;

        Exception translated = OracleExceptionTranslator.Translate(forged, Target);

        translated.Should().NotBeSameAs(forged);
        translated.Message.Should().NotContain(RowValue);
        translated.InnerException.Should().BeSameAs(forged);
    }

    [Test]
    public void ValidationFailure_ShouldNotAddAnythingToExceptionData_WhenItMarksAnException()
    {
        ArgumentException validation = OracleValidation.Argument("The table 'x' is not valid.", "TableName");

        validation.Data.Count.Should().Be(0);
        OracleValidation.IsValidationFailure(validation).Should().BeTrue();
    }

    [Test]
    public void Translate_ShouldWrap_WhenAnUnmarkedArgumentExceptionComesFromTheDriver()
    {
        var driver = new ArgumentException($"driver argument error {RowValue}");

        Exception translated = OracleExceptionTranslator.Translate(driver, Target);

        translated.Should().NotBeSameAs(driver);
        translated.Message.Should().NotContain(RowValue);
        translated.InnerException.Should().BeSameAs(driver);
    }

    [TestCase(1, "ORA-00001")]
    [TestCase(942, "ORA-00942")]
    [TestCase(39826, "ORA-39826")]
    public void ErrorCode_ShouldFormatFiveDigits_WhenGivenANumber(int number, string expected)
    {
        OracleExceptionTranslator.ErrorCode(number).Should().Be(expected);
    }

    [TestCase(904)]
    [TestCase(942)]
    public void Translate_ShouldLeaveOutTheCasingHint_WhenTheProviderUsesCatalogLookup(int number)
    {
        OracleException original = NewOracleException(number, $"ORA-{number:D5}: {RowValue}");

        Exception translated = OracleExceptionTranslator.Translate(original, Target, OracleIdentifierResolution.CatalogLookup);

        translated.Message.Should().NotContain("OracleIdentifierResolution.CatalogLookup").And.NotContain(RowValue);
        translated.InnerException.Should().BeSameAs(original);
    }

    [Test]
    public void FromNumber_ShouldExplainTheVarchar2BindLimit_WhenAStringIsTooLong()
    {
        var inner = new Exception();

        Exception translated = OracleExceptionTranslator.FromNumber(1461, inner, "Posts");

        translated.Message.Should().StartWith("Bulk copy to 'Posts' failed (Oracle error ORA-01461).")
            .And.Contain("4000 bytes").And.Contain("CLOB").And.NotContain("CatalogLookup");
        translated.InnerException.Should().BeSameAs(inner);
    }
}
