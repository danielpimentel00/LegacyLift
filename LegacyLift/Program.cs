using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));

builder.Services.AddSingleton<IProductEvents, RabbitMqProductEvents>();

var app = builder.Build();

app.MapGet("/api/products/{id:int}", async (int id, AppDbContext db, IDistributedCache cache) =>
{
    var key = $"product:{id}";

    var cached = await cache.GetStringAsync(key);
    if (cached is not null)
        return Results.Ok(JsonSerializer.Deserialize<Product>(cached));

    var product = await db.Products.FindAsync(id);
    if (product is null)
        return Results.NotFound();

    await cache.SetStringAsync(key, JsonSerializer.Serialize(product));
    return Results.Ok(product);
});

app.MapPost("/api/products", async (CreateProductRequest request, AppDbContext db, IProductEvents events) =>
{
    var product = new Product { Name = request.Name, Price = request.Price };
    db.Products.Add(product);
    await db.SaveChangesAsync();

    await events.PublishCreatedAsync(product);

    return Results.Created($"/api/products/{product.Id}", product);
});

app.Run();

public partial class Program { }
