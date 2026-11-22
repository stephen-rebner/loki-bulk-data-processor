using System;

namespace Loki.BulkDataProcessor.Oracle;

/// <summary>Settings for one Oracle provider registration, set in the <c>AddOracle(options => ...)</c> callback.</summary>
/// <remarks>The values are copied when <c>AddOracle</c> runs; changing this object afterwards has no effect.</remarks>
public sealed class OracleProviderOptions
{
    /// <summary>
    /// How rows are written. Default <see cref="OracleWriteMode.ArrayBind"/>. <see cref="OracleWriteMode.DirectPath"/> is for bulk loads into
    /// staging tables only (foreign keys are not enforced and other sessions' queries wait until the transaction ends); register it under
    /// its own provider name and read its hazards first.
    /// </summary>
    public OracleWriteMode WriteMode { get; set; } = OracleWriteMode.ArrayBind;

    /// <summary>How destination table and column names are matched. Default <see cref="OracleIdentifierResolution.Unquoted"/>.</summary>
    public OracleIdentifierResolution IdentifierResolution { get; set; } = OracleIdentifierResolution.Unquoted;

    /// <summary>How <see cref="bool"/> values are bound. Default <see cref="OracleBooleanBinding.Number"/>. Applies to
    /// <see cref="OracleWriteMode.ArrayBind"/> only: with <see cref="OracleWriteMode.DirectPath"/> ODP.NET converts the values itself.</summary>
    public OracleBooleanBinding BooleanBinding { get; set; } = OracleBooleanBinding.Number;

    /// <summary>Checks every value and returns an independent copy, so later changes to this object do not leak into the provider.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A property holds a value that is not defined by its enum.</exception>
    internal OracleProviderOptions Validated()
    {
        ThrowIfUndefined(WriteMode, nameof(WriteMode));
        ThrowIfUndefined(IdentifierResolution, nameof(IdentifierResolution));
        ThrowIfUndefined(BooleanBinding, nameof(BooleanBinding));
        return new OracleProviderOptions { WriteMode = WriteMode, IdentifierResolution = IdentifierResolution, BooleanBinding = BooleanBinding };
    }

    private static void ThrowIfUndefined<TEnum>(TEnum value, string propertyName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(propertyName, value, $"Unknown {typeof(TEnum).Name} value.");
        }
    }
}
