using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

app.MapGet("/api/products/{id:int}", async (int id, AppDbContext db) =>
    await db.Products.FindAsync(id) is { } product
        ? Results.Ok(product)
        : Results.NotFound());

app.MapPost("/api/products", async (CreateProductRequest request, AppDbContext db) =>
{
    var product = new Product { Name = request.Name, Price = request.Price };
    db.Products.Add(product);
    await db.SaveChangesAsync();

    return Results.Created($"/api/products/{product.Id}", product);
});

app.Run();

public partial class Program { }
