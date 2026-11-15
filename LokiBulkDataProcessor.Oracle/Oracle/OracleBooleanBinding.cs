namespace Loki.BulkDataProcessor.Oracle;

/// <summary>How <see cref="bool"/> source values are bound by the array-bind writer.</summary>
public enum OracleBooleanBinding
{
    /// <summary>
    /// Default. <c>true</c>/<c>false</c> bind as NUMBER 1/0: works with <c>NUMBER(1)</c> columns on 19c and later, and is
    /// implicitly converted to <c>BOOLEAN</c> on 23ai.
    /// </summary>
    Number = 0,

    /// <summary>Native <c>BOOLEAN</c> binding (Oracle Database 23ai and later only).</summary>
    Boolean = 1
}
