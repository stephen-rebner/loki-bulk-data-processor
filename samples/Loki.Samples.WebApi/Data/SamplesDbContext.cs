using Microsoft.EntityFrameworkCore;

namespace Loki.Samples.WebApi.Data;

/// <summary>
/// The SQL Server schema (Customers, Orders, Products), created by the EF Core migrations in <c>Migrations/</c>, and the context the
/// caller-owned transaction sample writes with. Oracle has the same tables in <c>samples/db/oracle/init.sql</c>.
/// </summary>
internal sealed class SamplesDbContext(DbContextOptions<SamplesDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(customer =>
        {
            customer.Property(c => c.Name).HasMaxLength(200);
            customer.Property(c => c.Email).HasMaxLength(320);
        });

        modelBuilder.Entity<Order>(order =>
        {
            order.Property(o => o.Reference).HasMaxLength(50);
            order.Property(o => o.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Product>(product =>
        {
            product.Property(p => p.Sku).HasMaxLength(50);
            product.Property(p => p.Name).HasMaxLength(200);
            product.Property(p => p.Price).HasPrecision(18, 2);
            product.HasIndex(p => p.Sku).IsUnique();
        });
    }
}
