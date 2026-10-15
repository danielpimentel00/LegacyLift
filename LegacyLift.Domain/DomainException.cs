namespace LegacyLift.Domain;

// Violacion de una regla de negocio. La API la traduce a 400 Bad Request.
public sealed class DomainException(string message) : Exception(message);
