using LegacyLift.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace LegacyLift.Functions;

public sealed record VolumeDiscountResponse(int Quantity, decimal Rate);

public sealed class VolumeDiscountFunction
{
    [Function(nameof(GetVolumeDiscount))]
    public IActionResult GetVolumeDiscount(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "volume-discount/{quantity:int}")]
        HttpRequest request,
        int quantity)
    {
        try
        {
            return new OkObjectResult(new VolumeDiscountResponse(quantity, VolumeDiscount.RateFor(quantity)));
        }
        catch (DomainException ex)
        {
            return new BadRequestObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Regla de negocio incumplida",
                Detail = ex.Message
            });
        }
    }
}
