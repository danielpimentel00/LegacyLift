using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace LegacyLift.Functions.Orders;

// El orquestador se re-ejecuta desde el principio (replay) cada vez que una actividad termina,
// por eso debe ser determinista: nada de DateTime.UtcNow, Guid.NewGuid, I/O ni servicios
// inyectados. Todo eso vive en las actividades.
public static class OrderSagaOrchestrator
{
    // Para fallos tecnicos (timeout, servicio caido). Un pago rechazado no es un fallo tecnico:
    // ChargePayment lo devuelve como resultado y no se reintenta.
    private static readonly TaskOptions StepRetry = TaskOptions.FromRetryPolicy(
        new RetryPolicy(maxNumberOfAttempts: 3, firstRetryInterval: TimeSpan.FromSeconds(2)));

    // Una compensacion que no se completa deja datos inconsistentes: se insiste mas.
    private static readonly TaskOptions CompensationRetry = TaskOptions.FromRetryPolicy(
        new RetryPolicy(maxNumberOfAttempts: 5, firstRetryInterval: TimeSpan.FromSeconds(2), backoffCoefficient: 2));

    [Function(nameof(ProcessOrder))]
    public static async Task<OrderResult> ProcessOrder([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var order = context.GetInput<OrderRequest>()!;
        var logger = context.CreateReplaySafeLogger(nameof(ProcessOrder));
        var compensations = new Stack<(string Activity, string Id)>();
        string shipmentId;

        try
        {
            context.SetCustomStatus("ReservandoInventario");
            var reservationId = await context.CallActivityAsync<string>(
                nameof(OrderActivities.ReserveInventory), order, StepRetry);
            compensations.Push((nameof(OrderActivities.ReleaseInventory), reservationId));

            context.SetCustomStatus("Cobrando");
            var payment = await context.CallActivityAsync<PaymentResult>(
                nameof(OrderActivities.ChargePayment), order, StepRetry);
            if (!payment.Approved)
                return await CompensateAsync(context, compensations, $"Pago rechazado: {payment.Reason}");
            compensations.Push((nameof(OrderActivities.RefundPayment), payment.PaymentId!));

            context.SetCustomStatus("CreandoEnvio");
            shipmentId = await context.CallActivityAsync<string>(
                nameof(OrderActivities.CreateShipment), order, StepRetry);
            compensations.Push((nameof(OrderActivities.CancelShipment), shipmentId));
        }
        catch (TaskFailedException ex)
        {
            logger.LogWarning("Pedido {InstanceId}: {TaskName} fallo tras los reintentos: {Error}",
                context.InstanceId, ex.TaskName, ex.FailureDetails.ErrorMessage);
            return await CompensateAsync(context, compensations, $"{ex.TaskName}: {ex.FailureDetails.ErrorMessage}");
        }

        // Punto sin retorno: el pedido ya esta confirmado. Si la notificacion falla no se
        // deshace la compra; se registra para reenviarla por otra via.
        context.SetCustomStatus("Notificando");
        try
        {
            await context.CallActivityAsync(nameof(OrderActivities.NotifyCustomer),
                new OrderConfirmation(order.CustomerEmail, shipmentId), StepRetry);
        }
        catch (TaskFailedException ex)
        {
            logger.LogWarning("Pedido {InstanceId} completado sin notificacion: {Error}",
                context.InstanceId, ex.FailureDetails.ErrorMessage);
        }

        context.SetCustomStatus("Completado");
        return new OrderResult("Completed", null, [], []);
    }

    // Deshace en orden inverso los pasos ya confirmados.
    private static async Task<OrderResult> CompensateAsync(TaskOrchestrationContext context,
        Stack<(string Activity, string Id)> compensations, string reason)
    {
        context.SetCustomStatus("Compensando");
        var compensated = new List<string>();
        var failures = new List<string>();

        while (compensations.TryPop(out var step))
        {
            try
            {
                await context.CallActivityAsync(step.Activity, step.Id, CompensationRetry);
                compensated.Add(step.Activity);
            }
            catch (TaskFailedException)
            {
                failures.Add(step.Activity);
            }
        }

        var status = failures.Count == 0 ? "Compensated" : "CompensationFailed";
        context.SetCustomStatus(status);
        return new OrderResult(status, reason, compensated, failures);
    }
}
