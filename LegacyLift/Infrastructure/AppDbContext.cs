using LegacyLift.Domain;
using Microsoft.EntityFrameworkCore;

namespace LegacyLift.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // El dominio no conoce EF: la configuración de persistencia vive aquí.
        modelBuilder.Entity<Product>(product =>
        {
            product.Property(p => p.Name).HasMaxLength(Product.NameMaxLength);
            product.Property(p => p.Price).HasPrecision(18, 2);
        });
    }
}
