using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace LegacyLift.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ProductsWriteTests
{
    private readonly SqliteWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductsWriteTests(SqliteWebApplicationFactory factory)
    {
        factory.ResetDatabase();
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("Ratón")]
    [InlineData("Webcam")]
    public async Task PostProduct_AddsExactlyOneProduct(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products", new { Name = name, Price = 19.99m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, db.Products.Count());
    }
}
