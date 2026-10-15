using System.Text.Json;
using LegacyLift.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));

builder.Services.AddSingleton<IProductEvents, RabbitMqProductEvents>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/api/products/{id:int}", async (int id, AppDbContext db, IDistributedCache cache) =>
{
    var key = $"product:{id}";

    var cached = await cache.GetStringAsync(key);
    if (cached is not null)
        return Results.Ok(JsonSerializer.Deserialize<ProductResponse>(cached));

    var product = await db.Products.FindAsync(id);
    if (product is null)
        return Results.NotFound();

    var response = ProductResponse.From(product);
    await cache.SetStringAsync(key, JsonSerializer.Serialize(response));
    return Results.Ok(response);
});

app.MapGet("/api/products/{id:int}/quote", async (int id, int quantity, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null)
        return Results.NotFound();

    return Results.Ok(new QuoteResponse(product.Id, quantity, product.QuoteTotal(quantity)));
});

app.MapPost("/api/products", async (CreateProductRequest request, AppDbContext db, IProductEvents events) =>
{
    var product = Product.Create(request.Name, request.Price);
    db.Products.Add(product);
    await db.SaveChangesAsync();

    await events.PublishCreatedAsync(product);

    return Results.Created($"/api/products/{product.Id}", ProductResponse.From(product));
});

app.Run();

public partial class Program { }
