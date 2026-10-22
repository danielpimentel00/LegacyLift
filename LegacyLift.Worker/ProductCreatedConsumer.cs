using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LegacyLift.Worker;

// Copia del contrato que publica la API: comparten el formato del mensaje, no el ensamblado.
public sealed record ProductCreated(int Id, string Name, decimal Price);

public sealed class ProductCreatedConsumer(IConnection connection, ILogger<ProductCreatedConsumer> logger)
    : BackgroundService
{
    private const string Queue = "product-created";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var message = JsonSerializer.Deserialize<ProductCreated>(delivery.Body.Span);

            logger.LogInformation("Producto creado: {ProductId} {ProductName} ({ProductPrice})",
                message?.Id, message?.Name, message?.Price);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
        };

        await channel.BasicConsumeAsync(Queue, autoAck: false, consumer, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
