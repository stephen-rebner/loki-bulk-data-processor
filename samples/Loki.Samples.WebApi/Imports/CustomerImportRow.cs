namespace Loki.Samples.WebApi.Imports;

/// <summary>
/// A customer as an import produces it. Its property names differ from the Customers columns on purpose:
/// <see cref="Mappings.CustomerImportRowMapping"/> maps them, and leaves out the identity column Id.
/// </summary>
internal sealed class CustomerImportRow
{
    public string FullName { get; set; } = string.Empty;

    public string? EmailAddress { get; set; }

    public DateTime RegisteredOn { get; set; }

    /// <summary>Not a column: a get-only property is never copied (README "Mappings").</summary>
    public string DisplayName => $"{FullName} <{EmailAddress}>";
}
