using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LegacyLift.Application.Products;

// IDistributedCache es una abstracción (Microsoft.Extensions.Caching.Abstractions), no Redis:
// Application puede usarla sin depender de la infraestructura concreta.
internal sealed class GetProductHandler(
    IProductRepository products,
    IDistributedCache cache,
    IOptionsMonitor<ProductCacheOptions> cacheOptions)
{
    public async Task<ProductResponse?> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var key = $"product:{id}";

        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
            return JsonSerializer.Deserialize<ProductResponse>(cached);

        var product = await products.FindAsync(id, cancellationToken);
        if (product is null)
            return null;

        var response = ProductResponse.From(product);
        await cache.SetStringAsync(key, JsonSerializer.Serialize(response),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheOptions.CurrentValue.TtlSeconds)
            },
            cancellationToken);
        return response;
    }
}
