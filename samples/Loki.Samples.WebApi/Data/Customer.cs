namespace Loki.Samples.WebApi.Data;

internal sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public DateTime CreatedOn { get; set; }

    public List<Order> Orders { get; set; } = [];
}
