using System.Text.Json;
using LegacyLift.Application.Products;
using LegacyLift.Domain;
using RabbitMQ.Client;

namespace LegacyLift.Infrastructure;

// Contrato del mensaje publicado en RabbitMQ (lo que leen los consumidores de la cola).
public sealed record ProductCreated(int Id, string Name, decimal Price);

public class RabbitMqProductEvents(IConfiguration configuration) : IProductEvents
{
    public const string Queue = "product-created";

    public async Task PublishCreatedAsync(Product product)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(configuration.GetConnectionString("RabbitMq")!)
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(Queue, durable: false, exclusive: false, autoDelete: false);
        await channel.BasicPublishAsync(
            exchange: "", routingKey: Queue, body: JsonSerializer.SerializeToUtf8Bytes(
                new ProductCreated(product.Id, product.Name, product.Price)));
    }
}
