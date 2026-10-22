using System.Text.Json;
using LegacyLift.Application.Products;
using LegacyLift.Domain;
using RabbitMQ.Client;

namespace LegacyLift.Infrastructure;

// Contrato del mensaje publicado en RabbitMQ (lo que leen los consumidores de la cola).
public sealed record ProductCreated(int Id, string Name, decimal Price);

public class RabbitMqProductEvents(IConnection connection) : IProductEvents
{
    public const string Queue = "product-created";

    public async Task PublishCreatedAsync(Product product)
    {
        await using var channel = await connection.CreateChannelAsync();

        // durable: RabbitMQ 4 no permite colas transitorias no exclusivas.
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false);
        await channel.BasicPublishAsync(
            exchange: "", routingKey: Queue, body: JsonSerializer.SerializeToUtf8Bytes(
                new ProductCreated(product.Id, product.Name, product.Price)));
    }
}
