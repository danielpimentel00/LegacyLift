using LegacyLift.Domain;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // El dominio no conoce EF: la configuracion de persistencia vive aqui.
        modelBuilder.Entity<Product>(product =>
        {
            product.Property(p => p.Name).HasMaxLength(Product.NameMaxLength);
            product.Property(p => p.Price).HasPrecision(18, 2);
        });
    }
}
