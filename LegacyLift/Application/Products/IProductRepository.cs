using LegacyLift.Domain;

namespace LegacyLift.Application.Products;

// Puerto de persistencia. Application define QUÉ necesita; Infrastructure decide CÓMO (EF Core).
public interface IProductRepository
{
    Task<Product?> FindAsync(int id, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);
}
