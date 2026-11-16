namespace Loki.BulkDataProcessor.Oracle;

/// <summary>How <see cref="bool"/> source values are bound by the array-bind writer.</summary>
public enum OracleBooleanBinding
{
    /// <summary>
    /// Default. <c>true</c>/<c>false</c> bind as NUMBER 1/0: works with <c>NUMBER(1)</c> columns on 19c and later. For a native
    /// <c>BOOLEAN</c> column on 23ai, prefer <see cref="Boolean"/>.
    /// </summary>
    Number = 0,

    /// <summary>Native <c>BOOLEAN</c> binding (Oracle Database 23ai and later only).</summary>
    Boolean = 1
}
