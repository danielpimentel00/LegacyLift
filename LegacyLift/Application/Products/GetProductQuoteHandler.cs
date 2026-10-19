namespace LegacyLift.Application.Products;

internal sealed class GetProductQuoteHandler(IProductRepository products)
{
    public async Task<QuoteResponse?> HandleAsync(int id, int quantity, CancellationToken cancellationToken)
    {
        var product = await products.FindAsync(id, cancellationToken);
        if (product is null)
            return null;

        return new QuoteResponse(product.Id, quantity, product.QuoteTotal(quantity));
    }
}
