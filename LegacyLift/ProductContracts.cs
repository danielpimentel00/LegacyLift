using LegacyLift.Domain;

// Contratos publicos de la API y del evento. Se mantienen separados de la entidad de dominio:
// la entidad puede cambiar sus invariantes sin romper a los clientes ni a los consumidores.
public record ProductResponse(int Id, string Name, decimal Price)
{
    public static ProductResponse From(Product product) => new(product.Id, product.Name, product.Price);
}

public record QuoteResponse(int ProductId, int Quantity, decimal Total);

public record ProductCreated(int Id, string Name, decimal Price);
