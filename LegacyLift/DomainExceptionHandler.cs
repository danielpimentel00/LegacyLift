using LegacyLift.Domain;
using Microsoft.AspNetCore.Diagnostics;

// Traduce las violaciones de reglas de negocio a 400 con ProblemDetails.
public class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
            return false;

        await Results.Problem(domainException.Message, statusCode: StatusCodes.Status400BadRequest)
            .ExecuteAsync(context);
        return true;
    }
}
