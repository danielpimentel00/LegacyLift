namespace LegacyLift.Functions;

// Los nombres de conexion coinciden con los recursos del AppHost: Aspire inyecta la
// configuracion de cada WithReference con ese nombre como prefijo.
internal static class Bindings
{
    public const string ServiceBusConnection = "servicebus";
    public const string BlobsConnection = "blobs";

    public const string ProductImportsQueue = "product-imports";
    public const string ProductImportsContainer = "product-imports";
}
