using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LegacyLift.IntegrationTests;

// Los mismos tests se ejecutan dos veces, una por estrategia de aislamiento.
// Cada clase derivada decide como se resetea el estado antes y despues de cada test.
public abstract class ProductsTests(ContainersWebApplicationFactory factory) : IAsyncLifetime
{
    protected ContainersWebApplicationFactory Factory { get; } = factory;
    private readonly HttpClient _client = factory.CreateClient();

    public abstract Task InitializeAsync();
    public virtual Task DisposeAsync() => Task.CompletedTask;

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

    [Fact]
    public async Task GetProduct_SecondRead_ComesFromRedis()
    {
        await _client.GetAsync("/api/products/1");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Products.Where(p => p.Id == 1)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, "Cambiado"));
        }

        var product = await _client.GetFromJsonAsync<Product>("/api/products/1");

        // La base dice "Cambiado"; si llega "Teclado", la respuesta salio de Redis.
        Assert.Equal("Teclado", product!.Name);
    }

    [Theory]
    [InlineData("Ratón")]
    [InlineData("Webcam")]
    public async Task PostProduct_AddsExactlyOneProduct(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products", new { Name = name, Price = 19.99m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, db.Products.Count());
    }

    [Fact]
    public async Task PostProduct_PublishesCreatedEvent()
    {
        await _client.PostAsJsonAsync("/api/products", new { Name = "Ratón", Price = 19.99m });

        var connectionFactory = new ConnectionFactory { Uri = new Uri(Factory.RabbitMqConnectionString) };
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var message = await channel.BasicGetAsync(RabbitMqProductEvents.Queue, autoAck: true);

        Assert.NotNull(message);
        var published = JsonSerializer.Deserialize<Product>(message.Body.Span);
        Assert.Equal("Ratón", published!.Name);
    }
}

[Collection(ApiCollection.Name)]
public class ProductsRespawnTests(ContainersWebApplicationFactory factory) : ProductsTests(factory)
{
    public override async Task InitializeAsync()
    {
        await Factory.ResetDatabaseWithRespawnAsync();
        await Factory.ResetCacheAndQueueAsync();
    }
}

[Collection(ApiCollection.Name)]
public class ProductsRollbackTests(ContainersWebApplicationFactory factory) : ProductsTests(factory)
{
    // xUnit ejecuta las clases de una coleccion una detras de otra, asi que basta con
    // dejar la base en su estado inicial (confirmado) una vez, al empezar esta clase.
    private static bool _baselineReady;

    public override async Task InitializeAsync()
    {
        if (!_baselineReady)
        {
            await Factory.ResetDatabaseWithRespawnAsync();
            _baselineReady = true;
        }

        await Factory.ResetCacheAndQueueAsync();
        await Factory.BeginTestTransactionAsync();
    }

    public override Task DisposeAsync() => Factory.RollbackTestTransactionAsync();
}
