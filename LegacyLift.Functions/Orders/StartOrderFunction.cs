using System.Net;
using System.Text.Json;
using LegacyLift.Domain;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace LegacyLift.Functions.Orders;

public static class StartOrderFunction
{
    // Devuelve 202 con las URLs para consultar el estado, enviar eventos o terminar la instancia.
    [Function(nameof(StartOrder))]
    public static async Task<HttpResponseData> StartOrder(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequestData request,
        [DurableClient] DurableTaskClient client)
    {
        OrderRequest? order;
        try
        {
            order = await request.ReadFromJsonAsync<OrderRequest>();
        }
        catch (JsonException)
        {
            order = null;
        }

        var error = order is null ? "El cuerpo del pedido no es valido." : Validate(order);
        if (error is not null)
        {
            // WriteAsJsonAsync fija el status en 200: se escribe el JSON a mano para conservar el 400.
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            badRequest.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await badRequest.WriteStringAsync(JsonSerializer.Serialize(new { error }));
            return badRequest;
        }

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(OrderSagaOrchestrator.ProcessOrder), order);

        return await client.CreateCheckStatusResponseAsync(request, instanceId);
    }

    private static string? Validate(OrderRequest order)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
            return "El email del cliente es obligatorio.";

        try
        {
            order.Total();
            return null;
        }
        catch (DomainException ex)
        {
            return ex.Message;
        }
    }
}
