using System;
using FluentAssertions;
using Loki.BulkDataProcessor.Oracle;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Oracle;

public class OracleProviderOptionsTests
{
    [Test]
    public void Constructor_ShouldUseTheDocumentedDefaults_WhenNothingIsSet()
    {
        var options = new OracleProviderOptions();

        options.WriteMode.Should().Be(OracleWriteMode.ArrayBind);
        options.IdentifierResolution.Should().Be(OracleIdentifierResolution.Unquoted);
        options.BooleanBinding.Should().Be(OracleBooleanBinding.Number);
    }

    [Test]
    public void EnumValues_ShouldBeStable_WhenPersistedOrBoundFromConfiguration()
    {
        ((int)OracleWriteMode.ArrayBind).Should().Be(0);
        ((int)OracleWriteMode.DirectPath).Should().Be(1);
        ((int)OracleIdentifierResolution.Unquoted).Should().Be(0);
        ((int)OracleIdentifierResolution.CatalogLookup).Should().Be(1);
        ((int)OracleBooleanBinding.Number).Should().Be(0);
        ((int)OracleBooleanBinding.Boolean).Should().Be(1);
    }

    [Test]
    public void Validated_ShouldReturnAnIndependentCopy_WhenTheValuesAreValid()
    {
        var options = new OracleProviderOptions
        {
            IdentifierResolution = OracleIdentifierResolution.CatalogLookup,
            BooleanBinding = OracleBooleanBinding.Boolean
        };

        OracleProviderOptions copy = options.Validated();
        options.IdentifierResolution = OracleIdentifierResolution.Unquoted;

        copy.Should().NotBeSameAs(options);
        copy.IdentifierResolution.Should().Be(OracleIdentifierResolution.CatalogLookup);
        copy.BooleanBinding.Should().Be(OracleBooleanBinding.Boolean);
        copy.WriteMode.Should().Be(OracleWriteMode.ArrayBind);
    }

    [Test]
    public void Validated_ShouldThrowArgumentOutOfRange_WhenTheWriteModeIsUndefined()
    {
        Action action = () => new OracleProviderOptions { WriteMode = (OracleWriteMode)42 }.Validated();

        action.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("WriteMode");
    }

    [Test]
    public void Validated_ShouldThrowArgumentOutOfRange_WhenTheIdentifierResolutionIsUndefined()
    {
        Action action = () => new OracleProviderOptions { IdentifierResolution = (OracleIdentifierResolution)(-1) }.Validated();

        action.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("IdentifierResolution");
    }

    [Test]
    public void Validated_ShouldThrowArgumentOutOfRange_WhenTheBooleanBindingIsUndefined()
    {
        Action action = () => new OracleProviderOptions { BooleanBinding = (OracleBooleanBinding)7 }.Validated();

        action.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("BooleanBinding");
    }

    [Test]
    public void Validated_ShouldThrowNotSupported_WhenDirectPathIsSelectedBeforeItIsAvailable()
    {
        Action action = () => new OracleProviderOptions { WriteMode = OracleWriteMode.DirectPath }.Validated();

        action.Should().Throw<NotSupportedException>().WithMessage("OracleWriteMode.DirectPath is not available*Use OracleWriteMode.ArrayBind*");
    }
}
