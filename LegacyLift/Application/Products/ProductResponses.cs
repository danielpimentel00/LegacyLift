using LegacyLift.Domain;

namespace LegacyLift.Application.Products;

// Lo que devuelven los handlers. La API los serializa tal cual, y los tests de integración los
// deserializan. Se mantienen separados de la entidad de dominio para que sus invariantes puedan
// cambiar sin romper a los clientes.
public sealed record ProductResponse(int Id, string Name, decimal Price)
{
    public static ProductResponse From(Product product) => new(product.Id, product.Name, product.Price);
}

public sealed record QuoteResponse(int ProductId, int Quantity, decimal Total);
