using System.Net;
using System.Net.Http.Json;

namespace LegacyLift.Functions.IntegrationTests;

[Collection(FunctionsCollection.Name)]
public class OrderSagaTests(FunctionsHostFixture fixture)
{
    // El caso de envio fallido incluye 3 intentos con espera antes de compensar.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private sealed record CheckStatusResponse(string Id, string StatusQueryGetUri);
    private sealed record OrchestrationStatus(string RuntimeStatus, OrderResult? Output);
    private sealed record OrderResult(
        string Status, string? Reason, List<string> Compensated, List<string> CompensationFailures);

    [Fact]
    public async Task ValidOrder_CompletesAllSteps()
    {
        var result = await RunOrderAsync(new
        {
            customerEmail = "ana@test.com", productName = "Teclado", unitPrice = 89.99m, quantity = 2
        });

        Assert.Equal("Completed", result.Status);
        Assert.Empty(result.Compensated);
    }

    [Fact]
    public async Task DeclinedPayment_ReleasesInventory()
    {
        // 60 x 100 = 6000, con 5% de descuento = 5700 > limite de 5000.
        var result = await RunOrderAsync(new
        {
            customerEmail = "ana@test.com", productName = "Monitor", unitPrice = 100m, quantity = 60
        });

        Assert.Equal("Compensated", result.Status);
        Assert.StartsWith("Pago rechazado", result.Reason);
        Assert.Equal(new[] { "ReleaseInventory" }, result.Compensated);
    }

    [Fact]
    public async Task ShippingFailure_RefundsAndReleasesInReverseOrder()
    {
        var result = await RunOrderAsync(new
        {
            customerEmail = "ana@test.com", productName = "Mouse", unitPrice = 25m, quantity = 1,
            simulateShippingFailure = true
        });

        Assert.Equal("Compensated", result.Status);
        Assert.Equal(new[] { "RefundPayment", "ReleaseInventory" }, result.Compensated);
        Assert.Empty(result.CompensationFailures);
    }

    [Fact]
    public async Task InvalidOrder_IsRejectedBeforeStartingTheOrchestration()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/orders", new
        {
            customerEmail = "ana@test.com", productName = "Mouse", unitPrice = 25m, quantity = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<OrderResult> RunOrderAsync(object order)
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/orders", order);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var started = await response.Content.ReadFromJsonAsync<CheckStatusResponse>();
        var deadline = DateTime.UtcNow + Timeout;

        while (DateTime.UtcNow < deadline)
        {
            var status = await fixture.Client.GetFromJsonAsync<OrchestrationStatus>(started!.StatusQueryGetUri);
            switch (status!.RuntimeStatus)
            {
                case "Completed":
                    return status.Output!;
                case "Failed" or "Terminated":
                    Assert.Fail($"La orquestacion {started.Id} termino como {status.RuntimeStatus}.");
                    break;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"La orquestacion {started!.Id} no termino a tiempo.");
    }
}
