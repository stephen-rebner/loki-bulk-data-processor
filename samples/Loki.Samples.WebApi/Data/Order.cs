namespace Loki.Samples.WebApi.Data;

internal sealed class Order
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public string Reference { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime OrderedOn { get; set; }
}
