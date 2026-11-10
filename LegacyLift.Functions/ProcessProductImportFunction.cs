using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LegacyLift.Domain;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LegacyLift.Functions;

public sealed class ProcessProductImportFunction(ILogger<ProcessProductImportFunction> logger)
{
    // AutoCompleteMessages = false: la funcion decide como liquidar cada mensaje.
    // Un error permanente (mensaje corrupto o regla de negocio) va directo a la dead-letter queue;
    // cualquier otra excepcion se propaga y Service Bus lo reintenta hasta MaxDeliveryCount.
    [Function(nameof(ProcessProductImport))]
    public async Task ProcessProductImport(
        [ServiceBusTrigger(Bindings.ProductImportsQueue, Connection = Bindings.ServiceBusConnection,
            AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        ProductImportRequested? request;
        try
        {
            request = message.Body.ToObjectFromJson<ProductImportRequested>();
        }
        catch (JsonException ex)
        {
            await DeadLetterAsync(message, messageActions, "MensajeInvalido", ex.Message, cancellationToken);
            return;
        }

        if (request is null)
        {
            await DeadLetterAsync(message, messageActions, "MensajeInvalido", "Cuerpo vacio", cancellationToken);
            return;
        }

        try
        {
            var product = Product.Create(request.Name, request.Price);

            logger.LogInformation("Producto importado: {ProductName} ({ProductPrice}) desde {SourceFile}:{Line}",
                product.Name, product.Price, request.SourceFile, request.Line);

            await messageActions.CompleteMessageAsync(message, cancellationToken);
        }
        catch (DomainException ex)
        {
            await DeadLetterAsync(message, messageActions, "ReglaDeNegocio", ex.Message, cancellationToken);
        }
    }

    private async Task DeadLetterAsync(ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions,
        string reason, string description, CancellationToken cancellationToken)
    {
        logger.LogWarning("Mensaje {MessageId} enviado a dead-letter: {Reason} - {Description}",
            message.MessageId, reason, description);

        await messageActions.DeadLetterMessageAsync(message, deadLetterReason: reason,
            deadLetterErrorDescription: description, cancellationToken: cancellationToken);
    }
}
