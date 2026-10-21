using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LegacyLift.Worker;

// Copia del contrato que publica la API (LegacyLift.Infrastructure.ProductCreated). El worker no
// referencia la API: comparten el formato del mensaje, no el ensamblado.
public sealed record ProductCreated(int Id, string Name, decimal Price);

public sealed class ProductCreatedConsumer(IConfiguration configuration, ILogger<ProductCreatedConsumer> logger)
    : BackgroundService
{
    private const string Queue = "product-created";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(configuration.GetConnectionString("RabbitMq")!)
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Mismos argumentos que la API: si el worker arranca primero, la cola ya existe cuando la
        // API publique (y si no coincidieran, RabbitMQ rechazaria la declaracion).
        // durable: true porque RabbitMQ 4 ya no permite colas transitorias no exclusivas.
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

        // El consumo ocurre en los callbacks; aqui solo se mantiene viva la conexion hasta el apagado.
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
