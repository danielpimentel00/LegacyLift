using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LegacyLift.Functions.Orders;

// Servicios externos simulados. Las actividades se ejecutan "al menos una vez": en un sistema
// real cada una deberia ser idempotente (por ejemplo, usando el id de la orquestacion como clave).
public sealed class OrderActivities(ILogger<OrderActivities> logger)
{
    public const decimal PaymentLimit = 5_000m;

    [Function(nameof(ReserveInventory))]
    public string ReserveInventory([ActivityTrigger] OrderRequest order)
    {
        var reservationId = $"res-{Guid.NewGuid():N}";
        logger.LogInformation("Inventario reservado {ReservationId}: {Quantity} x {ProductName}",
            reservationId, order.Quantity, order.ProductName);
        return reservationId;
    }

    [Function(nameof(ChargePayment))]
    public PaymentResult ChargePayment([ActivityTrigger] OrderRequest order)
    {
        var total = order.Total();
        if (total > PaymentLimit)
        {
            logger.LogWarning("Pago rechazado por {Total}", total);
            return new PaymentResult(false, null, $"el total {total} supera el limite de {PaymentLimit}");
        }

        var paymentId = $"pay-{Guid.NewGuid():N}";
        logger.LogInformation("Pago {PaymentId} aprobado por {Total}", paymentId, total);
        return new PaymentResult(true, paymentId, null);
    }

    [Function(nameof(CreateShipment))]
    public string CreateShipment([ActivityTrigger] OrderRequest order)
    {
        if (order.SimulateShippingFailure)
            throw new InvalidOperationException("Transportista no disponible");

        var shipmentId = $"shp-{Guid.NewGuid():N}";
        logger.LogInformation("Envio {ShipmentId} creado", shipmentId);
        return shipmentId;
    }

    [Function(nameof(NotifyCustomer))]
    public void NotifyCustomer([ActivityTrigger] OrderConfirmation confirmation) =>
        logger.LogInformation("Confirmacion enviada a {CustomerEmail} (envio {ShipmentId})",
            confirmation.CustomerEmail, confirmation.ShipmentId);

    [Function(nameof(ReleaseInventory))]
    public void ReleaseInventory([ActivityTrigger] string reservationId) =>
        logger.LogInformation("Compensacion: reserva {ReservationId} liberada", reservationId);

    [Function(nameof(RefundPayment))]
    public void RefundPayment([ActivityTrigger] string paymentId) =>
        logger.LogInformation("Compensacion: pago {PaymentId} reembolsado", paymentId);

    [Function(nameof(CancelShipment))]
    public void CancelShipment([ActivityTrigger] string shipmentId) =>
        logger.LogInformation("Compensacion: envio {ShipmentId} cancelado", shipmentId);
}
