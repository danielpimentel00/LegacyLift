using LegacyLift.Application.Products;

namespace LegacyLift.Api;

// Los endpoints solo traducen HTTP <-> handlers. No conocen EF Core, Redis ni RabbitMQ:
// una regla de arquitectura lo comprueba (LegacyLift.ArchitectureTests).
internal static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{id:int}", async (
            int id, GetProductHandler handler, CancellationToken cancellationToken) =>
        {
            var product = await handler.HandleAsync(id, cancellationToken);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        app.MapGet("/api/products/{id:int}/quote", async (
            int id, int quantity, GetProductQuoteHandler handler, CancellationToken cancellationToken) =>
        {
            var quote = await handler.HandleAsync(id, quantity, cancellationToken);
            return quote is null ? Results.NotFound() : Results.Ok(quote);
        });

        app.MapPost("/api/products", async (
            CreateProductRequest request, CreateProductHandler handler, CancellationToken cancellationToken) =>
        {
            var product = await handler.HandleAsync(request.Name, request.Price, cancellationToken);
            return Results.Created($"/api/products/{product.Id}", product);
        });

        return app;
    }
}
