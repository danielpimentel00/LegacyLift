using System.Net;
using System.Net.Http.Json;

namespace LegacyLift.Functions.IntegrationTests;

[Collection(FunctionsCollection.Name)]
public class HttpAndTimerTests(FunctionsHostFixture fixture)
{
    // Copia del contrato: los tests ven la Function como caja negra, igual que un cliente.
    private sealed record VolumeDiscountResponse(int Quantity, decimal Rate);
    private sealed record ProblemDetails(int Status, string Title, string Detail);

    [Theory]
    [InlineData(9, 0.00)]
    [InlineData(10, 0.05)]
    [InlineData(100, 0.10)]
    public async Task GetVolumeDiscount_ReturnsRateFromDomain(int quantity, double expectedRate)
    {
        var response = await fixture.Client.GetFromJsonAsync<VolumeDiscountResponse>(
            $"/api/volume-discount/{quantity}");

        Assert.Equal((decimal)expectedRate, response!.Rate);
    }

    [Fact]
    public async Task GetVolumeDiscount_InvalidQuantity_ReturnsProblemDetails()
    {
        var response = await fixture.Client.GetAsync("/api/volume-discount/0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.False(string.IsNullOrWhiteSpace(problem!.Detail));
    }

    [Fact]
    public async Task NightlyCatalogReport_RunsWhenInvokedThroughAdminApi()
    {
        var response = await fixture.Client.PostAsJsonAsync("/admin/functions/NightlyCatalogReport", new { input = "" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await fixture.WaitForLogAsync("Reporte de catalogo ejecutado", TimeSpan.FromSeconds(30));
    }
}
