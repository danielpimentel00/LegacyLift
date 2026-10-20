using LegacyLift.Domain;

namespace LegacyLift.Application.Products;

internal sealed class CreateProductHandler(IProductRepository products, IProductEvents events)
{
    public async Task<ProductResponse> HandleAsync(string name, decimal price, CancellationToken cancellationToken)
    {
        var product = Product.Create(name, price);

        await products.AddAsync(product, cancellationToken);
        await events.PublishCreatedAsync(product);

        return ProductResponse.From(product);
    }
}
