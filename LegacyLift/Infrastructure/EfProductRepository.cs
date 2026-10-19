using LegacyLift.Application.Products;
using LegacyLift.Domain;

namespace LegacyLift.Infrastructure;

internal sealed class EfProductRepository(AppDbContext db) : IProductRepository
{
    public async Task<Product?> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.Products.FindAsync([id], cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
    }
}
