namespace LegacyLift.Domain.Tests;

internal static class DomainAssert
{
    // Una regla de negocio violada debe lanzar DomainException (no cualquier Exception)
    // y llevar un mensaje util: la API lo devuelve tal cual en el ProblemDetails.
    // No se compara el texto exacto para no acoplar los tests a la redaccion.
    public static DomainException Violates(Action action)
    {
        var exception = Assert.Throws<DomainException>(action);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        return exception;
    }
}
