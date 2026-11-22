namespace Loki.Samples.WebApi.Data;

internal sealed class Product
{
    public int Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }
}
