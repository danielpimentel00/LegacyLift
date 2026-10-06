using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace LegacyLift.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ProductsReadTests
{
    private readonly HttpClient _client;

    public ProductsReadTests(SqliteWebApplicationFactory factory)
    {
        factory.ResetDatabase();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProduct_ReturnsSeededProduct()
    {
        var product = await _client.GetFromJsonAsync<Product>("/api/products/1");

        Assert.Equal("Teclado", product!.Name);
    }

    [Fact]
    public async Task GetProduct_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/products/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
