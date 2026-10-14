using System.Text.Json;
using RabbitMQ.Client;

public interface IProductEvents
{
    Task PublishCreatedAsync(Product product);
}

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
            exchange: "", routingKey: Queue, body: JsonSerializer.SerializeToUtf8Bytes(product));
    }
}
