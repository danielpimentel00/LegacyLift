using LegacyLift.Domain;

namespace LegacyLift.Functions.Orders;

public sealed record OrderRequest(
    string CustomerEmail,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    bool SimulateShippingFailure = false)
{
    public decimal Total() => Product.Create(ProductName, UnitPrice).QuoteTotal(Quantity);
}

public sealed record PaymentResult(bool Approved, string? PaymentId, string? Reason);

public sealed record OrderConfirmation(string CustomerEmail, string ShipmentId);

public sealed record OrderResult(
    string Status,
    string? Reason,
    IReadOnlyList<string> Compensated,
    IReadOnlyList<string> CompensationFailures);
