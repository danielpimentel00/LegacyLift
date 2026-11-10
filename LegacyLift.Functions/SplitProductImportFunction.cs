using System.Globalization;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LegacyLift.Functions;

public sealed class SplitProductImportFunction(ILogger<SplitProductImportFunction> logger)
{
    // Un CSV "nombre,precio" subido al contenedor se reparte en un mensaje por linea.
    // Aqui solo se valida el formato; las reglas de negocio las aplica el consumidor de la cola.
    [Function(nameof(SplitProductImport))]
    [ServiceBusOutput(Bindings.ProductImportsQueue, Connection = Bindings.ServiceBusConnection)]
    public string[] SplitProductImport(
        [BlobTrigger(Bindings.ProductImportsContainer + "/{name}", Connection = Bindings.BlobsConnection)]
        string content,
        string name)
    {
        var messages = new List<string>();
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
                continue;

            var columns = line.Split(',');
            if (columns.Length != 2
                || !decimal.TryParse(columns[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
            {
                logger.LogWarning("Linea {Line} de {BlobName} ignorada por formato invalido", i + 1, name);
                continue;
            }

            messages.Add(JsonSerializer.Serialize(new ProductImportRequested(columns[0].Trim(), price, name, i + 1)));
        }

        logger.LogInformation("{BlobName}: {MessageCount} productos enviados a la cola", name, messages.Count);
        return messages.ToArray();
    }
}
