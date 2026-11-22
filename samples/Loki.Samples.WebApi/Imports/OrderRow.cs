namespace Loki.Samples.WebApi.Imports;

/// <summary>
/// An order for the Orders table. No <c>ModelMapping</c>: every property with a public getter and setter is copied to the column
/// of the same name (README "Mappings"). It has no Id, because Orders.Id is an identity column.
/// </summary>
internal sealed class OrderRow
{
    public int CustomerId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime OrderedOn { get; set; }
}
