using System;
using System.Collections.Generic;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;

namespace Loki.BulkDataProcessor.Oracle.Internal.Types;

/// <summary>How one source column is bound: the parameter type and the conversion applied to each non-null value.</summary>
internal sealed record OracleBinding(OracleDbType DbType, Func<object, object> Convert);

/// <summary>
/// Maps a source column's .NET type to an <see cref="OracleDbType"/> and a value conversion for the array-bind writer.
/// Unsupported types (for example <c>object</c>) throw before anything is written. Immutable; one instance per provider.
/// </summary>
internal sealed class OracleTypeMap(OracleBooleanBinding booleanBinding)
{
    private static readonly Func<object, object> AsIs = value => value;

    private static readonly IReadOnlyDictionary<Type, OracleBinding> Bindings = new Dictionary<Type, OracleBinding>
    {
        [typeof(string)] = new(OracleDbType.Varchar2, AsIs),            // "" is stored as NULL (Oracle semantics)
        [typeof(char)] = new(OracleDbType.Varchar2, value => value.ToString()),
        [typeof(byte)] = new(OracleDbType.Byte, AsIs),
        [typeof(sbyte)] = new(OracleDbType.Int16, value => (short)(sbyte)value),
        [typeof(short)] = new(OracleDbType.Int16, AsIs),
        [typeof(ushort)] = new(OracleDbType.Int32, value => (int)(ushort)value),
        [typeof(int)] = new(OracleDbType.Int32, AsIs),
        [typeof(uint)] = new(OracleDbType.Int64, value => (long)(uint)value),
        [typeof(long)] = new(OracleDbType.Int64, AsIs),
        [typeof(ulong)] = new(OracleDbType.Decimal, value => (decimal)(ulong)value),
        [typeof(decimal)] = new(OracleDbType.Decimal, AsIs),
        [typeof(double)] = new(OracleDbType.BinaryDouble, AsIs),
        [typeof(float)] = new(OracleDbType.BinaryFloat, AsIs),
        [typeof(DateTime)] = new(OracleDbType.TimeStamp, AsIs),
        [typeof(DateTimeOffset)] = new(OracleDbType.TimeStampTZ, AsIs),
        [typeof(TimeSpan)] = new(OracleDbType.IntervalDS, AsIs),
        [typeof(Guid)] = new(OracleDbType.Raw, value => ((Guid)value).ToByteArray()),   // RAW(16), .NET byte order (same as direct path)
        [typeof(byte[])] = new(OracleDbType.Raw, AsIs),                                 // up to the RAW limit; BLOB is not supported
    };

    private readonly OracleBinding _boolean = booleanBinding == OracleBooleanBinding.Boolean
        ? new OracleBinding(OracleDbType.Boolean, AsIs)
        : new OracleBinding(OracleDbType.Int16, value => (bool)value ? (short)1 : (short)0);

    public OracleBooleanBinding BooleanBinding { get; } = booleanBinding;

    /// <summary>The binding for a source column of <paramref name="fieldType"/> (nullable types use their underlying type).</summary>
    /// <exception cref="NotSupportedException">The type cannot be array-bound (marked as a validation failure).</exception>
    public OracleBinding For(Type fieldType, string sourceColumn)
    {
        Type type = fieldType is null ? typeof(object) : Nullable.GetUnderlyingType(fieldType) ?? fieldType;
        if (type == typeof(bool))
        {
            return _boolean;
        }

        if (type.IsEnum)
        {
            return ForEnum(type);
        }

        return Bindings.TryGetValue(type, out OracleBinding binding) ? binding : throw Unsupported(type, sourceColumn);
    }

    /// <summary>Enums bind as their underlying integer type.</summary>
    private static OracleBinding ForEnum(Type enumType)
    {
        Type underlying = Enum.GetUnderlyingType(enumType);
        OracleBinding numeric = Bindings[underlying];
        return new OracleBinding(numeric.DbType,
            value => numeric.Convert(System.Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture)));
    }

    private static NotSupportedException Unsupported(Type type, string sourceColumn) =>
        OracleValidation.NotSupported(
            $"The source column '{SafeText.Sanitize(sourceColumn)}' has type {type.Name}, which the Oracle array-bind writer does not support.");
}
